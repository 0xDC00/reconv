#if BRIDGE_DOTNET
using Bridge;
#else
using System.Runtime.Serialization;
#endif
using System.Text;
using BufLib.Common.IO;
using static BufLib.TextFormats.BinaryModels.Catherine.BMD;

namespace BufLib.TextFormats.DataModels
{
    public class Catherine
    {
        public class LineInfo
        {
            /// <summary>
            /// Start
            /// </summary>
            public string S { get; set; }

            /// <summary>
            /// End
            /// </summary>
            public string E { get; set; }
        }

        #region BMD
#if BRIDGE_DOTNET
        public class MSGHeaderS
        {
            [Name("T")]
            public int Type { get; set; }
            [Name("N")]
            public short NumLine { get; set; }
            [Name("S")]
            public short SpeakerIndex { get; set; }
            [Name("H")]
            public string Title { get; set; }

            //public MSGHeader ToMSGHeader()
            //{
            //    return new MSGHeader()
            //    {
            //        Type = (MSGType)Type,
            //        Title = Encoding.ASCII.GetBytes(Title).Align(0x20),
            //        NumLine = NumLine,
            //        SpeakerIndex = SpeakerIndex
            //    };
            //}
        }
#else
        [DataContract]
        public class MSGHeaderS
        {
            // đổi tên member để chuổi json ngắn gọn.
            [DataMember(Name = "T")]
            public int Type { get; set; }
            [DataMember(Name = "N")]
            public short NumLine { get; set; }
            [DataMember(Name = "S")]
            public short SpeakerIndex { get; set; }
            [DataMember(Name = "H")]
            public string Title { get; set; }

            public MSGHeader ToMSGHeader()
            {
                return new MSGHeader()
                {
                    Type = (MSGType)Type,
                    Title = Encoding.ASCII.GetBytes(Title).Align(0x20),
                    NumLine = NumLine,
                    SpeakerIndex = SpeakerIndex
                };
            }
        }
#endif
        #endregion
    }
}
