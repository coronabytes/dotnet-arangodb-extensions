namespace Core.Arango.Linq
{
    public static partial class Aql
    {
        [AqlFunction("CONCAT")]
        public static string Concat(params string[] str)
        {
            throw e;
        }

        [AqlFunction("CONCAT_SEPARATOR")]
        public static string ConcatSeparator(string separator, params string[] str)
        {
            throw e;
        }

        [AqlFunction("CHAR_LENGTH")]
        public static int CharLength(string value)
        {
            throw e;
        }

        [AqlFunction("LOWER")]
        public static string Lower(string value)
        {
            throw e;
        }

        [AqlFunction("UPPER")]
        public static string Upper(string value)
        {
            throw e;
        }

        [AqlFunction("SUBSTITUTE")]
        public static string Substitute(string value, string search, string replace)
        {
            throw e;
        }

        [AqlFunction("SUBSTITUTE")]
        public static string Substitute(string value, string search, string replace, int limit)
        {
            throw e;
        }

        [AqlFunction("SUBSTRING")]
        public static string Substring(string value, int offset)
        {
            throw e;
        }

        [AqlFunction("SUBSTRING")]
        public static string Substring(string value, int offset, int length)
        {
            throw e;
        }

        [AqlFunction("LEFT")]
        public static string Left(string value, int length)
        {
            throw e;
        }

        [AqlFunction("RIGHT")]
        public static string Right(string value, int length)
        {
            throw e;
        }

        [AqlFunction("TRIM")]
        public static string Trim(string value)
        {
            throw e;
        }

        [AqlFunction("TRIM")]
        public static string Trim(string value, int type)
        {
            throw e;
        }

        [AqlFunction("TRIM")]
        public static string Trim(string value, string chars)
        {
            throw e;
        }

        [AqlFunction("LTRIM")]
        public static string LTrim(string value)
        {
            throw e;
        }

        [AqlFunction("LTRIM")]
        public static string LTrim(string value, string chars)
        {
            throw e;
        }

        [AqlFunction("RTRIM")]
        public static string RTrim(string value)
        {
            throw e;
        }

        [AqlFunction("RTRIM")]
        public static string RTrim(string value, string chars)
        {
            throw e;
        }

        [AqlFunction("SPLIT")]
        public static string[] Split(string value)
        {
            throw e;
        }

        [AqlFunction("SPLIT")]
        public static string[] Split(string value, string separator)
        {
            throw e;
        }

        [AqlFunction("SPLIT")]
        public static string[] Split(string value, string separator, int limit)
        {
            throw e;
        }

        [AqlFunction("REVERSE")]
        public static string Reverse(string value)
        {
            throw e;
        }

        [AqlFunction("CONTAINS")]
        public static bool Contains(string text, string search)
        {
            throw e;
        }

        [AqlFunction("CONTAINS")]
        public static int Contains(string text, string search, bool returnIndex)
        {
            throw e;
        }

        [AqlFunction("FIND_FIRST")]
        public static int FindFirst(string text, string search)
        {
            throw e;
        }

        [AqlFunction("FIND_FIRST")]
        public static int FindFirst(string text, string search, int start, int end)
        {
            throw e;
        }

        [AqlFunction("FIND_LAST")]
        public static int FindLast(string text, string search)
        {
            throw e;
        }

        [AqlFunction("FIND_LAST")]
        public static int FindLast(string text, string search, int start, int end)
        {
            throw e;
        }

        [AqlFunction("LIKE")]
        public static bool Like(string text, string search)
        {
            throw e;
        }

        [AqlFunction("LIKE")]
        public static bool Like(string text, string search, bool caseInsensitive)
        {
            throw e;
        }
    }
}