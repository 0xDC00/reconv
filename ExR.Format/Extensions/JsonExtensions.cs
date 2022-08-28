namespace ExR.Format
{
    public static class JsonExtensions
    {
        public static string ToJson<T>(this T o)
        {
            return SpanJson.JsonSerializer.Generic.Utf16.Serialize(o);
            //return System.Text.Json.JsonSerializer.Serialize(obj);
        }

        public static T FromJson<T>(this string s)
        {
            return SpanJson.JsonSerializer.Generic.Utf16.Deserialize<T>(s);
            //return return System.Text.Json.JsonSerializer.Deserialize<T>(json);
        }

        //public static string ToCSV<T>(this T o)
        //{
        //    return null;
        //}

        //public static T FromCSV<T>(this string s)
        //{
        //    return default(T);
        //}
    }
}
