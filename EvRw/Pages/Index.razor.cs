// TODO: refactor
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using System;
using System.IO;
using Microsoft.AspNetCore.Components.Forms;
using System.Collections.Generic;
using Microsoft.JSInterop;
using System.Linq;
using ExR;
using static EvRw.Program;
using ExR.Format;
using System.Reflection;
using System.IO.Compression;

namespace EvRw.Pages
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Components.Routing;
    using Microsoft.JSInterop;

    public partial class Index : ComponentBase
    {
        //[Inject] public Blazor.DownloadFileFast.Interfaces.IBlazorDownloadFileService BlazorDownloadFileService { get; set; }

        [Inject] private IJSRuntime JS { get; set; }

        // https://docs.microsoft.com/en-us/aspnet/core/blazor/host-and-deploy/configure-trimmer?view=aspnetcore-5.0
        // https://docs.microsoft.com/en-us/aspnet/core/blazor/webassembly-performance-best-practices?view=aspnetcore-5.0
        private IJSInProcessRuntime JSIn => (IJSInProcessRuntime)JS;
        private IJSUnmarshalledRuntime JSUn => (IJSUnmarshalledRuntime)JS;

        bool IsTaskRunning = false;

        //[JSInvokable("ExRw")]
        async Task StartProcess()
        {
            //var bytes = await JS.InvokeAsync<byte[]>("someJSMethodReturningAByteArray");
            //function someJSMethodReturningAByteArray() { return new Uint8Array([1, 2, 3]); }

            // Task.Run(async () => {})
            // Blazor 1 thread, Any 'heavy operation' will block the UI, that is the expected behaviour.
            if (!IsTaskRunning)
            {
                JSIn.InvokeVoid("console.clear");
                PluginSelectOnChange(new ChangeEventArgs()
                {
                    Value = formatValueCmd
                });
                IsTaskRunning = true;
                _PackResult = null;

                var _textFormat = InitFormat(formatValueCmd);
                if (_textFormat == null)
                {
                    Log.Error("-g");
                }
                else
                {
                    _textFormat.JSIn = JSIn;
                    _textFormat.JSUn = JSUn;

                    var stopwatch = new System.Diagnostics.Stopwatch();
                    stopwatch.Start();

                    var conv = new TextConv
                    {
                        Log = Log,
                        Convert = _textFormat
                    };

                    var baseNameWithoutExtensions = inputFileName.Split('.', 2)[0];

                    // TODO: https://github.com/Tewr/BlazorWorker/issues/50
                    MemoryStream result;
                    if (!isRepack)
                    {
                        outputFileName = baseNameWithoutExtensions + "_extracted.zip";
                        Log.Debug($"ExR {RadioValueAction} {formatValueCmd} -i {inputFileName} -o {outputFileName}");
                        if (inputFileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                        {
                            using (var xlsx = inputStream.ToZip(inputFileName))
                            {
                                result = await conv.Extract(xlsx);
                            }
                        }
                        else
                        {
                            result = await conv.Extract(inputStream);
                        }
                    }
                    else
                    {
                        outputFileName = baseNameWithoutExtensions + "_repacked.zip";
                        Log.Debug($"ExR {RadioValueAction} {formatValueCmd} -i {inputFileName} -o {outputFileName}");
                        if (inputFileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                        {
                            // JS parse xlsx -> FS
                            Log.Info("Reading sheets...");
                            var pathAndData = JSUn.InvokeUnmarshalled<byte[], string[]>("LoadXlsxUn", inputStream.GetBuffer());
                            result = await conv.RepackXlsx(pathAndData, outputStream);
                        }
                        else
                        {
                            result = await conv.Repack(inputStream, outputStream);
                        }
                    }

                    // check result
                    using (result)
                    {
                        if (result.IsZip())
                        {
                            using (var zip = new ZipArchive(result, ZipArchiveMode.Read, true))
                            {
                                int entryIndex = -1; // 0 file
                                for (int i = 0; i < zip.Entries.Count; i++)
                                {
                                    var e = zip.Entries[i];
                                    if (e.Length > 0)
                                    {
                                        if (entryIndex == -1)
                                        {
                                            entryIndex = i; // i [0..]
                                        }
                                        else
                                        {
                                            entryIndex = -2; // >1 file
                                            break;
                                        }
                                    }
                                }

                                if (entryIndex > -1)
                                {
                                    var entry = zip.Entries[entryIndex];
                                    if (entry.Name == entry.FullName)
                                    {
                                        _PackResult = new byte[entry.Length];
                                        entry.Open().Read(_PackResult, 0, _PackResult.Length);

                                        var outnames = entry.Name.Split('.', 2);
                                        if (outnames[0] == "_PACK_" && outnames.Length == 2)
                                        {
                                            outputFileName = baseNameWithoutExtensions + '.' + outnames[1];
                                        }
                                        else
                                        {
                                            outputFileName = entry.Name;
                                        }
                                    }
                                    else
                                    {
                                        _PackResult = result.ToArray();
                                    }

                                    Log.Info(outputFileName + " " + FormatSize(_PackResult.Length, 2));
                                }
                                else if (entryIndex == -1)
                                {
                                    Log.Warning("No result.");
                                    _PackResult = null;
                                }
                                else
                                {
                                    _PackResult = result.ToArray();
                                    Log.Info(outputFileName + " " + FormatSize(_PackResult.Length, 2));
                                }
                            }
                        }
                        else if (result.Length > 0) // ???
                        {
                            if (isRepack)
                                outputFileName = Path.ChangeExtension(inputFileName, null);
                            else
                                outputFileName = inputFileName + ".csv";
                            _PackResult = result.ToArray();
                            Log.Info(outputFileName + " " + FormatSize(_PackResult.Length, 2));
                        }
                        else
                        {
                            Log.Warning("No result.");
                            _PackResult = null;
                        }
                    }


                    stopwatch.Stop();
                    Console.WriteLine();
                    Log.Debug(string.Format("Time Elapsed {0:hh\\:mm\\:ss}", stopwatch.Elapsed));
                    Log.Info("Build success");
                }

                IsTaskRunning = false;
            }
            Log.Debug("Done!");
        }

        bool readMe = false;
        bool lazyReadMe = false;
        byte[] _PackResult = null;
        //async Task StartDownload()
        void StartDownload()
        {
            if (_PackResult != null)
            {
                // BlazorDownloadFileFast
                // "application/zip"
                // "application/octet-stream"
                //await BlazorDownloadFileService.DownloadFileAsync(outputFileName, _PackResult, "application/octet-stream");
                _ = JSUn.InvokeUnmarshalled<string, string, byte[], bool>("downLoadFileFastUn", outputFileName, "application/octet-stream", _PackResult);
                _PackResult = null;
            }
        }

        //protected override async Task OnInitializedAsync()
        //{
        //    await Task.Delay(5000);
        //    var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
        //    string path = uri.GetLeftPart(UriPartial.Path); // http://localhost:11232/wwwroot/readme
        //    if (path.EndsWith("/readme", StringComparison.OrdinalIgnoreCase))
        //    {
        //        //path = uri.GetLeftPart(UriPartial.Authority) + '/' + uri.Query;
        //        path = path.Substring(0, path.Length - 6) + uri.Query; // remove readme
        //        JSIn.InvokeVoid("window.history.replaceState", null, null, path);
        //    }
        //}

        //protected override async Task OnInitializedAsync()\
        protected override void OnInitialized()
        {
            dictFomat = new Dictionary<string, FormatInfo>(StringComparer.OrdinalIgnoreCase);

            var typeInfo = typeof(TextFormat).GetTypeInfo();
            // GetTypesInNamespace
            var types = typeInfo.Assembly.GetTypes()
                .Where(t => string.Equals(t.Namespace, typeInfo.Namespace, StringComparison.Ordinal));

            var typeofPluginAtt = typeof(PluginAttribute);
            foreach (var type in types)
            {
                var att = type.GetCustomAttribute(typeofPluginAtt);
                if (att != null)
                {
                    var meta = (PluginAttribute)att;
                    dictFomat[meta.Command] = new FormatInfo()
                    {
                        Cmd = meta.Command,
                        Dest = meta.Description,
                        Name = meta.Name,
                        Type = type
                    };
                }
            }
            dictFomat = new Dictionary<string, FormatInfo>(dictFomat.OrderBy(x => x.Value.Name));
            Console.WriteLine("Loaded: " + dictFomat.Count + " plugin(s).");

            // first time home page: https://dotnetfiddle.net/OvjmED
            var uri = _NavigationManager.ToAbsoluteUri(_NavigationManager.Uri);
            string path = uri.GetLeftPart(UriPartial.Path).TrimEnd('/'); // http://localhost:11232/wwwroot/readme
            if (path.EndsWith("/readme", StringComparison.OrdinalIgnoreCase))
            {
                //path = uri.GetLeftPart(UriPartial.Authority) + '/' + uri.Query;
                var home = path.Substring(0, path.Length - 6) + uri.Query; // remove readme
                // window.history.replaceState will not affect NavigationManager.Uri
                // => RefeshUrl will wrong
                // JSIn.InvokeVoid("window.history.replaceState", null, null, path);

                //// https://stackoverflow.com/questions/58076758/navigationerror-on-navigateto
                //// OnAfterRender instead of OnInitialized ?
                //NavigationManager.NavigateTo(home); // push history (no HandleLocationChanged event yet)
                readMe = true;
                lazyReadMe = true;

                // set current is HOME and navigate to README (for goback HOME)
                formatValueCmd = dictFomat.FirstOrDefault().Key;
                _path = home;
                home = home + "?" + RadioValueAction[1] + '=' + formatValueCmd;
                JSIn.InvokeVoid("window.history.replaceState", null, null, home);
                //_NavigationManager.NavigateTo(home, replace: true); // .net 6

                _NavigationManager.NavigateTo(path); // push history readMe
            }
            else
            {
                TryLoadFormatFromUrl();
            }

            _NavigationManager.LocationChanged += HandleLocationChanged;
            //await base.OnInitializedAsync();
        }

        TextFormat InitFormat(string cmd)
        {
            if (dictFomat.TryGetValue(cmd, out var format))
            {
                return (TextFormat)Activator.CreateInstance(format.Type);
            }
            else
            {
                return null;
            }
        }

        class FormatInfo
        {
            public string Cmd { get; set; }
            public string Name { get; set; }
            public string Dest { get; set; }
            public Type Type { get; set; }
        }

        Dictionary<string, FormatInfo> dictFomat;
        private string formatValueCmd; // { get; set; } // game command
        MemoryStream inputStream = null;
        string inputFileName = string.Empty;

        Stream outputStream = null;
        string outputFileName = string.Empty;

        const string defaultStatusExtract = "Input (any*)";
        const string defaultStatusRepack = "Input (text)";
        const string defaultStatusOutput = "Output (optional; any*)";
        const string titleRepack = "Repack | ReImport";
        const string titleExtract = "Extract";
        string currentTitle = titleRepack;
        string RadioValueAction = "-r";
        bool isRepack = true;


        string statusInput = defaultStatusRepack;
        string statusOutput = defaultStatusOutput;
        string dropClassInput = string.Empty;
        string dropClassOutput = string.Empty;

        public void RadioValueActionOnChange(ChangeEventArgs args)
        {
            _PackResult = null;

            // only two value
            //RadioValueAction = args.Value.ToString();
            //isRepack = RadioValueAction == "-r";
            isRepack = !isRepack;

            if (isRepack)
            {
                RadioValueAction = "-r";
                currentTitle = titleRepack;
                if (inputStream == null)
                {
                    statusInput = defaultStatusRepack;
                }
            }
            else
            {
                RadioValueAction = "-e";
                currentTitle = titleExtract;
                if (inputStream == null)
                {
                    statusInput = defaultStatusExtract;
                }

                if (outputStream != null)
                {
                    outputStream.Close();
                    outputStream = null;
                    statusOutput = defaultStatusOutput;
                    outputFileName = string.Empty;
                }
            }

            RefeshUrlWithoutHistoryAndEvent(); // unpack/repack radio refesh dll
        }

        public void PluginSelectOnChange(ChangeEventArgs args)
        {
            _PackResult = null;
            IsTaskRunning = false;
            JSIn.InvokeVoid("_clear");
            formatValueCmd = args.Value.ToString();
            var plug = dictFomat[formatValueCmd];
            Log.Info(plug.Name);
            Log.Info(plug.Cmd);
            Log.Warning(plug.Dest);

            RefeshUrlWithoutHistoryAndEvent(); // plugin select refesh dll
        }

        // switch mode
        // switch plugin
        void RefeshUrlWithoutHistoryAndEvent()
        {
            if (!readMe)
            {
                //if (_path == null)
                //{
                //    var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
                //    _path = uri.GetLeftPart(UriPartial.Path);
                //}

                //var param = DecodeQueryParameters(uri);
                var url = _path + "?" + RadioValueAction[1] + '=' + formatValueCmd;
                //loadFormat = false;
                // https://github.com/dotnet/aspnetcore/issues/25540
                //_NavigationManager.NavigateTo(path);
                JSIn.InvokeVoid("window.history.replaceState", null, null, url); // no HandleLocationChanged; loadFormat = true;
                //_NavigationManager.NavigateTo(url, false, true); // .net 6

            }
        }
        string _path = null;
        //bool loadFormat = true;

        void TryLoadFormatFromUrl()
        {
            // OnoInit => load formatValueCmd
            // OnAfterRender => Update Dropdown
            var uri = _NavigationManager.ToAbsoluteUri(_NavigationManager.Uri);
            _path = uri.GetLeftPart(UriPartial.Path);

            var param = DecodeQueryParameters(uri);
            if (param.Count > 0)
            {
                if (param.TryGetValue("e", out string cmde))
                {
                    statusInput = defaultStatusExtract;
                    currentTitle = titleExtract;
                    RadioValueAction = "-e";
                    isRepack = false;
                    formatValueCmd = cmde;
                    // https://stackoverflow.com/questions/14804253/how-to-set-selected-value-on-select-using-selectpicker-plugin-from-bootstrap
                    // $('.selectpicker').selectpicker('val', YOUR_VALUE); // Use brackets [YOUR_VALUE] for multiple selected values
                    // vs value="formatValueCmd"
                    // JSIn.InvokeVoid("SetSelectPickerValue", "#selectPlugin", formatValueCmd);
                }
                else if (param.TryGetValue("r", out string cmdr))
                {
                    // default repack
                    //statusInput = defaultStatusRepack;
                    //RadioValueAction = "-r";
                    //isRepack = true;
                    formatValueCmd = cmdr;
                    // JSIn.InvokeVoid("SetSelectPickerValue", "#selectPlugin", formatValueCmd);
                }
                else
                {
                    // default repack
                    formatValueCmd = dictFomat.FirstOrDefault().Key;
                    return;
                }

                if (!dictFomat.ContainsKey(formatValueCmd))
                {
                    formatValueCmd = dictFomat.FirstOrDefault().Key;
                }
            }
            else
            {
                formatValueCmd = dictFomat.FirstOrDefault().Key;
            }
        }

        // only on click on ReadMe button 
        private void HandleLocationChanged(object sender, LocationChangedEventArgs e)
        {
            //var uri = NavigationManager.ToAbsoluteUri(e.Location);
            //string path = uri.GetLeftPart(UriPartial.Path).TrimEnd('/'); // http://localhost:11232/wwwroot/readme
            //readMe = path.EndsWith("/readme", StringComparison.OrdinalIgnoreCase);
            readMe = !readMe; // only two page => no need check
            lazyReadMe = true;

            //var s = e.Location.Replace("://", string.Empty).Split('/', 2);
            //readMe = (s.Length == 2 && s[1].Split('?', 2)[0].Equals("readme", StringComparison.Ordinal));

            StateHasChanged(); // refesh ui

            //if (loadFormat)
            //{
            //    LoadFormatFromUrl();
            //    JSIn.InvokeVoid("SetSelectPickerValue", "#selectPlugin", formatValueCmd);
            //    StateHasChanged();
            //}
            //Log.Info(string.Format("URL of new location: {Location}", e.Location));
        }

        public void Dispose()
        {
            _NavigationManager.LocationChanged -= HandleLocationChanged;
        }

        static Dictionary<string, string> DecodeQueryParameters(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException("uri");

            if (uri.Query.Length == 0)
                return new Dictionary<string, string>();

            return uri.Query.TrimStart('?')
                            .Split(new[] { '&', ';' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(parameter => parameter.Split(new[] { '=' }, StringSplitOptions.RemoveEmptyEntries))
                            .GroupBy(parts => parts[0],
                                     parts => parts.Length > 2 ? string.Join("=", parts, 1, parts.Length - 1) : (parts.Length > 1 ? parts[1] : ""))
                            .ToDictionary(grouping => grouping.Key,
                                          grouping => string.Join(",", grouping));
        }

        async Task OnInputFileChange(InputFileChangeEventArgs e)
        {
            if (e.FileCount > 0)
            {
                if (inputStream != null)
                {
                    inputStream.Close();
                }

                dropClassInput = string.Empty;

                var file = e.File;
                Log.Debug("Open: " + file.Name);
                using (var stream = file.OpenReadStream(file.Size))
                {
                    inputFileName = file.Name;
                    inputStream = new MemoryStream((int)file.Size);
                    await stream.CopyToAsync(inputStream); // memory access out of bounds | Out of memory (1>gb)
                    inputStream.Position = 0;
                }

                statusInput = $"{file.Name} ({FormatSize(file.Size, 2)})";
                Log.Debug(statusInput);
                if (isRepack && file.Name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    // Do nothing
                }
                else if (inputStream.IsZip() == false)
                {
                    // zip all input
                    // -e dat => zip
                    // -r csv => zip
                    Log.Debug("->ZipFS: " + inputFileName);
                    inputStream = inputStream.ToZip(inputFileName); // >1GB Out of memory
                    statusInput += "*";
                }

                _PackResult = null;
            }
        }

        async Task OnOutputFileChange(InputFileChangeEventArgs e)
        {
            if (e.FileCount > 0)
            {
                if (outputStream != null)
                {
                    outputStream.Close();
                }

                var file = e.File;
                Log.Debug("Open: " + e.File);
                using (var stream = file.OpenReadStream(file.Size))
                {
                    outputFileName = file.Name;
                    outputStream = new MemoryStream((int)file.Size);
                    await stream.CopyToAsync(outputStream);
                    outputStream.Position = 0;
                }

                statusOutput = $"{file.Name} ({FormatSize(file.Size, 2)})";
                Log.Debug(statusOutput);
                if (outputStream.IsZip() == false)
                {
                    // -r output -> zip
                    Log.Debug("->ZipFS: " + outputFileName);
                    outputStream = outputStream.ToZip(outputFileName);
                    statusOutput += "*";
                }

                _PackResult = null;
            }
        }

        //protected override Task OnAfterRenderAsync(bool firstRender)
        protected override void OnAfterRender(bool firstRender)
        {
            if (firstRender)
            {
                JSIn.InvokeVoid("InitTerminal");
                JSIn.InvokeVoid("InitSelectPickerPlugin");

                JSIn.InvokeVoid("SetSelectPickerValue", "#selectPlugin", formatValueCmd);
                PluginSelectOnChange(new ChangeEventArgs()
                {
                    Value = formatValueCmd
                });
            }

            //await base.OnAfterRenderAsync(firstRender);
        }

        private static string FormatSize(long size, int decimals = 0)
        {
            if (size == 0) return "0 Byte";

            var sizes = new string[] { "Bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

            int k = 1024;
            var i = (int)Math.Floor(Math.Log(size) / Math.Log(k));
            if (i >= sizes.Length)
                return string.Empty;

            int dm = decimals <= 0 ? 0 : decimals;
            var result = (size / Math.Pow(k, i)).ToString("N" + dm) + ' ' + sizes[i];
            return result;
        }
    }
}

