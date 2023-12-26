using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Xml;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Runtime.InteropServices;
using System.Data.Common;
using System.Collections.Concurrent;
using System.Collections;
using System.Runtime.Serialization.Json;
using System.Net;

namespace ProducerConsumer;

public static class Utils
{
    #region [Reusable HttpClient]
    static HttpClient? _httpClient = null;
    public static HttpClient? httpClient 
    { 
        get {
            if (_httpClient == null)
                _httpClient = new HttpClient();

            return _httpClient;
        }
        private set { _httpClient = value; }
    }
    #endregion

    public static async Task<bool> DownloadFileAndSave(string url, CancellationToken token)
    {
        try
        {
            string fileName = Path.GetFileName(url);
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c.ToString(), "_");
            }

            if (string.IsNullOrEmpty(fileName))
                fileName = Path.GetTempFileName();

            //string downloadsFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads";
            string downloadsFolder = KnownFolders.GetKnownPath(KnownFolders.Downloads);
            string finalPath = Path.Combine(downloadsFolder, fileName);

            var data = await httpClient.GetByteArrayAsync(url, token);
            if (data != null && data.Length > 0)
            {
                Console.WriteLine($"Download complete.");
                await File.WriteAllBytesAsync(finalPath, data);
                Console.WriteLine($"Saved \"{fileName}\"");
                return true;
            }
            else
            {
                Console.WriteLine($"No data to write.");
                return false;
            }
        }
        catch (WebException ex)
        {
            Console.WriteLine($"[DownloadFile]: {ex.Status} - {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"[DownloadFile]: Operation was canceled.");
        }
        catch (Exception e)
        {
            Console.WriteLine($"[DownloadFile]: {e.Message}");
        }
        return false;
    }

    /// <summary>
    /// For <see cref="System.Drawing.Icon"/> code.
    /// </summary>
    /// <returns>true if Windows 7 or higher, false otherwise</returns>
    public static bool IsWindowsCompatible()
    {
        if (Environment.OSVersion.Platform == PlatformID.Win32NT && 
            Environment.OSVersion.Version >= new Version(6, 1))
            return true;
        
        return false;
    }

    /// <summary>
    /// Converts the <see cref="DateTime"/> to amount in the past.
    /// </summary>
    public static string ToTimeAgo(this DateTime baseTime)
    {
        var _timeSpan = DateTime.Now - baseTime;

        if (_timeSpan.TotalMinutes <= 0.1d)
            return "just now";

        if (_timeSpan.TotalMinutes <= 1.0d)
            return $"{_timeSpan.TotalSeconds:N0} seconds ago";

        if (_timeSpan.TotalMinutes < 60d)
            return $"{_timeSpan.TotalMinutes:N0} minutes ago";

        if (_timeSpan.TotalHours < 2d)
            return $"{_timeSpan.TotalHours:N0} hour ago";

        if (_timeSpan.TotalHours < 24d)
            return $"{_timeSpan.TotalHours:N0} hours ago";

        if (_timeSpan.TotalDays < 2d)
            return $"{_timeSpan.TotalDays:N0} day ago";

        if (_timeSpan.TotalDays < 7d)
            return $"{_timeSpan.TotalDays:N0} days ago";

        if (_timeSpan.TotalDays >= 7d && _timeSpan.TotalDays < 29d)
        {
            int weeks = Convert.ToInt32(Math.Floor((double)_timeSpan.Days / 7d));
            return weeks <= 1 ? "1 week ago" : weeks + " weeks ago";
        }

        if (_timeSpan.TotalDays > 29d && _timeSpan.TotalDays < 365d)
        {
            int months = Convert.ToInt32(Math.Floor((double)_timeSpan.Days / 29d));
            return months <= 1 ? "1 month ago" : months + " months ago";
        }

        if (Convert.ToDouble(_timeSpan.TotalDays / 365d) < 2d)
            return "1 year ago";

        return Convert.ToDouble(_timeSpan.TotalDays / 365d).ToString("#") + " years ago";
    }

    /// <summary>
    /// Displays a readable sentence as to when the time happened.
    /// e.g. 'One second ago' or '2 months ago'
    /// </summary>
    public static string ToTimeAgo(this DateTime value, bool useUTC = false)
    {
        TimeSpan ts;

        if (useUTC)
            ts = new TimeSpan(DateTime.UtcNow.Ticks - value.Ticks);
        else
            ts = new TimeSpan(DateTime.Now.Ticks - value.Ticks);

        double delta = ts.TotalSeconds;
        if (delta < 60)
            return ts.Seconds == 1 ? "one second ago" : ts.Seconds + " seconds ago";
        if (delta < 120)
            return "a minute ago";
        if (delta < 2700) // 45 * 60
            return ts.Minutes + " minutes ago";
        if (delta < 5400) // 90 * 60
            return "an hour ago";
        if (delta < 86400) // 24 * 60 * 60
            return ts.Hours + " hours ago";
        if (delta < 172800) // 48 * 60 * 60
            return "yesterday";
        if (delta < 2592000) // 30 * 24 * 60 * 60
            return ts.Days + " days ago";
        if (delta < 31104000) // 12 * 30 * 24 * 60 * 60
        {
            int months = Convert.ToInt32(Math.Floor((double)ts.Days / 30));
            return months <= 1 ? "one month ago" : months + " months ago";
        }
        int years = Convert.ToInt32(Math.Floor((double)ts.Days / 365));
        return years <= 1 ? "one year ago" : years + " years ago";
    }

    /// <summary>
    /// Similar to <see cref="GetReadableTime(TimeSpan)"/>.
    /// </summary>
    /// <param name="timeSpan"><see cref="TimeSpan"/></param>
    /// <returns>formatted text</returns>
    public static string ToReadableString(this TimeSpan span)
    {
        //return string.Format("{0}{1}{2}{3}",
        //    span.Duration().Days > 0 ? string.Format("{0:0} day{1}, ", span.Days, span.Days == 1 ? string.Empty : "s") : string.Empty,
        //    span.Duration().Hours > 0 ? string.Format("{0:0} hr{1}, ", span.Hours, span.Hours == 1 ? string.Empty : "s") : string.Empty,
        //    span.Duration().Minutes > 0 ? string.Format("{0:0} min{1}, ", span.Minutes, span.Minutes == 1 ? string.Empty : "s") : string.Empty,
        //    span.Duration().Seconds > 0 ? string.Format("{0:0} sec{1}", span.Seconds, span.Seconds == 1 ? string.Empty : "s") : string.Empty);

        var parts = new StringBuilder();
        if (span.Days > 0)
            parts.Append($"{span.Days} day{(span.Days == 1 ? string.Empty : "s")} ");
        if (span.Hours > 0)
            parts.Append($"{span.Hours} hour{(span.Hours == 1 ? string.Empty : "s")} ");
        if (span.Minutes > 0)
            parts.Append($"{span.Minutes} minute{(span.Minutes == 1 ? string.Empty : "s")} ");
        if (span.Seconds > 0)
            parts.Append($"{span.Seconds} second{(span.Seconds == 1 ? string.Empty : "s")} ");
        if (span.Milliseconds > 0)
            parts.Append($"{span.Milliseconds} millisecond{(span.Milliseconds == 1 ? string.Empty : "s")} ");

        return parts.ToString().Trim();
    }

    /// <summary>
    /// Sometimes "TimeOfDay" is not the clearest meaning for TimeSpan, this makes it explicit.
    /// </summary>
    /// <param name="dateTime"><see cref="DateTime"/></param>
    /// <returns><see cref="TimeSpan"/></returns>
    public static TimeSpan ToTimeSpan(this DateTime dateTime) => dateTime.TimeOfDay;

    /// <summary>
    /// The lexical representation for duration is the ISO8601 extended format PnYn MnDTnH nMnS,<br/>
    /// where nY represents the number of years, nM the number of months, nD the number of days,<br/>
    /// 'T' is the date/time separator, nH the number of hours, nM the number of minutes and nS the<br/>
    /// number of seconds. The number of seconds can include decimal digits to arbitrary precision.<br/>
    /// An optional preceding minus sign ('-') is allowed, to indicate a negative duration.<br/>
    /// If the sign is omitted a positive duration is indicated.<br/>
    /// <see href="http://www.w3.org/TR/xmlschema-2/#duration"/><br/>
    /// </summary>
    /// <example>
    /// <code>TimeSpan ts = System.Xml.XmlConvert.ToTimeSpan("PT72H");</code>
    /// </example>
    /// <returns><see cref="TimeSpan"/></returns>
    public static TimeSpan ToTimeSpan(this string isoDuration)
    {
        try
        {
            var ts = System.Xml.XmlConvert.ToTimeSpan(isoDuration);
            return ts;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ToTimeSpan: {ex.Message}");
            return TimeSpan.Zero;
        }
    }

    public static void Deconstruct(this DateTime dateTime, out int year, out int month, out int day) => (year, month, day) = (dateTime.Year, dateTime.Month, dateTime.Day);
    public static void Deconstruct(this DateTime dateTime, out int year, out int month, out int day, out int hour, out int minute, out int second) => (year, month, day, hour, minute, second) = (dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, dateTime.Second);
    public static TimeSpan Multiply(this TimeSpan timeSpan, double scalar) => new TimeSpan((long)(timeSpan.Ticks * scalar));
    public static DateTime ConvertToLastDayOfMonth(this DateTime date) => new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));
    public static DateTime StartOfMonth(this DateTime dateTime) => dateTime.Date.AddDays(-1 * (dateTime.Day - 1));
    public static DateTime EndOfMonth(this DateTime dateTime) => new DateTime(dateTime.Year, dateTime.Month, DateTime.DaysInMonth(dateTime.Year, dateTime.Month));

    /// <summary>
    /// Similar to <see cref="GetReadableTime(DateTime, bool)"/>.
    /// </summary>
    /// <param name="timeSpan"><see cref="TimeSpan"/></param>
    /// <returns>formatted text</returns>
    public static string GetReadableTime(this TimeSpan timeSpan)
    {
        var ts = new TimeSpan(DateTime.Now.Ticks - timeSpan.Ticks);
        var totMinutes = ts.TotalSeconds / 60;
        var totHours = ts.TotalSeconds / 3_600;
        var totDays = ts.TotalSeconds / 86_400;
        var totWeeks = ts.TotalSeconds / 604_800;
        var totMonths = ts.TotalSeconds / 2_592_000;
        var totYears = ts.TotalSeconds / 31_536_000;

        var parts = new StringBuilder();
        if (totYears > 0.1)
            parts.Append($"{totYears:N1} years ");
        if (totMonths > 0.1)
            parts.Append($"{totMonths:N1} months ");
        if (totWeeks > 0.1)
            parts.Append($"{totWeeks:N1} weeks ");
        if (totDays > 0.1)
            parts.Append($"{totDays:N1} days ");
        if (totHours > 0.1)
            parts.Append($"{totHours:N1} hours ");
        if (totMinutes > 0.1)
            parts.Append($"{totMinutes:N1} minutes ");

        return parts.ToString().Trim();
    }

    /// <summary>
    /// Similar to <see cref="GetReadableTime(TimeSpan)"/>.
    /// </summary>
    /// <param name="timeSpan"><see cref="TimeSpan"/></param>
    /// <returns>formatted text</returns>
    public static string GetReadableTime(this DateTime dateTime, bool addMilliseconds = false)
    {
        var timeSpan = new TimeSpan(DateTime.Now.Ticks - dateTime.Ticks);
        //double totalSecs = timeSpan.TotalSeconds;

        var parts = new StringBuilder();
        if (timeSpan.Days > 0)
            parts.AppendFormat("{0} {1} ", timeSpan.Days, timeSpan.Days == 1 ? "day" : "days");
        if (timeSpan.Hours > 0)
            parts.AppendFormat("{0} {1} ", timeSpan.Hours, timeSpan.Hours == 1 ? "hour" : "hours");
        if (timeSpan.Minutes > 0)
            parts.AppendFormat("{0} {1} ", timeSpan.Minutes, timeSpan.Minutes == 1 ? "minute" : "minutes");
        if (timeSpan.Seconds > 0)
            parts.AppendFormat("{0} {1} ", timeSpan.Seconds, timeSpan.Seconds == 1 ? "second" : "seconds");
        if (addMilliseconds && timeSpan.Milliseconds > 0)
            parts.AppendFormat("{0} {1}", timeSpan.Milliseconds, timeSpan.Milliseconds == 1 ? "millisecond" : "milliseconds");

        return parts.ToString().TrimEnd();
    }

    /// <summary>
    /// Check to see if a date is between two dates.
    /// </summary>
    /// <param name="dt"></param>
    /// <param name="rangeBeg"></param>
    /// <param name="rangeEnd"></param>
    /// <returns>true if between range, false otherwise</returns>
    public static bool Between(this DateTime dt, DateTime rangeBeg, DateTime rangeEnd)
    {
        return dt.Ticks >= rangeBeg.Ticks && dt.Ticks <= rangeEnd.Ticks;
    }

    /// <summary>
    /// Gets a <see cref="DateTime"/> object representing the time until midnight.
    /// </summary>
    /// <example>
    /// var hoursUntilMidnight = TimeUntilMidnight().TimeOfDay.TotalHours;
    /// </example>
    public static DateTime TimeUntilMidnight()
    {
        DateTime now = DateTime.Now;
        DateTime midnight = now.Date.AddDays(1);
        TimeSpan timeUntilMidnight = midnight - now;
        return new DateTime(timeUntilMidnight.Ticks);
    }

    /// <summary>
    /// Returns a range of <see cref="DateTime"/> objects matching the criteria provided.
    /// </summary>
    /// <example>
    /// IEnumerable<DateTime> dateRange = DateTime.Now.GetDateRangeTo(DateTime.Now.AddDays(80));
    /// </example>
    /// <param name="self"><see cref="DateTime"/></param>
    /// <param name="toDate"><see cref="DateTime"/></param>
    /// <returns><see cref="IEnumerable{DateTime}"/></returns>
    public static IEnumerable<DateTime> GetDateRangeTo(this DateTime self, DateTime toDate)
    {
        var range = Enumerable.Range(0, new TimeSpan(toDate.Ticks - self.Ticks).Days);

        return from p in range select self.Date.AddDays(p);
    }

    /// <summary>
    /// Determine if the date is a working day.
    /// </summary>
    public static bool WorkingDay(this DateTime date)
    {
        return date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday;
    }
    /// <summary>
    /// Determine if the date is a weekend.
    /// </summary>
    public static bool IsWeekend(this DateTime date)
    {
        return date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
    }
    /// <summary>
    /// Determine the next workday coming up.
    /// </summary>
    public static DateTime NextWorkday(this DateTime date)
    {
        DateTime nextDay = date.AddDays(1);
        while (!nextDay.WorkingDay())
        {
            nextDay = nextDay.AddDays(1);
        }
        return nextDay;
    }

    /// <summary>
    /// Attempt to tryparse a date string.
    /// If successful the new DateTime seconds will be added to the 1/1/1970 object.
    /// </summary>
    /// <param name="value"></param>
    /// <returns><see cref="DateTime"/> object if successful, null otherwise</returns>
    public static DateTime? ConvertNumberToDate(this string value)
    {
        DateTime _time = new DateTime(1970, 1, 1, 0, 0, 0, 0);

        if (!string.IsNullOrEmpty(value))
        {
            double dateTime;
            if (Double.TryParse(value, out dateTime))
                return _time.AddSeconds(dateTime);
            else
                return null;
        }
        else
            return null;
    }

    /// <summary>
    /// Helper for walking up the folder tree.
    /// </summary>
    public static string NavigateUpFolders(string basePath, int step = 2)
    {
        if (string.IsNullOrEmpty(basePath))
            basePath = Environment.CurrentDirectory;

        if (step < 1)
            return basePath;

        for (int i = 0; i < step; i++)
        {
            var tmp = Directory.GetParent(basePath)?.FullName;
            if (!string.IsNullOrEmpty(tmp))
                basePath = tmp;
        }

        #region [Alternative]
        //var stack = string.Empty;
        //for (int i = 0; i < step; i++) { stack += @"..\"; }
        //var newPath = Path.GetFullPath(Path.Combine(basePath, stack));
        #endregion

        return basePath;
    }

    /// <summary>
    /// A random <see cref="Boolean"/> generator.
    /// </summary>
    public static bool CoinFlip() => (Rnd.Next(100) > 49) ? true : false;

    /// <summary>
    /// An updated string truncation helper.
    /// </summary>
    public static string Truncate(this string text, int maxLength, string mesial = "…")
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        if (maxLength > 0 && text.Length > maxLength)
        {
            var limit = maxLength / 2;
            if (limit > 1)
            {
                return String.Format("{0}{1}{2}", text.Substring(0, limit).Trim(), mesial, text.Substring(text.Length - limit).Trim());
            }
            else
            {
                var tmp = text.Length <= maxLength ? text : text.Substring(0, maxLength).Trim();
                return String.Format("{0}{1}", tmp, mesial);
            }
        }
        return text;
    }

    /// <summary>
    /// A string truncation helper.
    /// </summary>
    public static string TruncateOld(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        // Using LINQ:
        //return value.Length <= maxLength ? value : new string(value.Take(maxLength).ToArray());

        // Using Substring:
        var tmp = value.Length <= maxLength ? value : value.Substring(0, maxLength);
        return $"{tmp}...";
    }

    /// <summary>
    /// Extracts elements seperated by spaces.
    /// </summary>
    public static string[] SplitStringBySpaces(this string text)
    {
        return Regex.Split(text, @"\s+");
        // Traditional method, but not very flexible.
        return text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// Extracts elements contained in braces and seperated by spaces.
    /// </summary>
    public static List<string> SplitStringBySpacesAndBraces(this string text)
    {
        var matches = Regex.Matches(text, @"{[^}]*}|[^ ]+");
        var parts = new List<string>();
        foreach (Match match in matches)
        {
            parts.Add(match.Value);
        }
        return parts;
    }

    /// <summary>
    /// ExampleTextSample => Example Text Sample
    /// </summary>
    /// <param name="input"></param>
    /// <returns>space delimited string</returns>
    public static string SeparateCamelCase(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        StringBuilder result = new StringBuilder();
        result.Append(input[0]);

        for (int i = 1; i < input.Length; i++)
        {
            if (char.IsUpper(input[i]))
                result.Append(' ');

            result.Append(input[i]);
        }

        return result.ToString();
    }

    /// <summary>
    /// Helper for parsing command line arguments.
    /// </summary>
    /// <param name="inputArray"></param>
    /// <returns>string array of args excluding the 1st arg</returns>
    public static string[] IgnoreFirstTakeRest(this string[] inputArray)
    {
        if (inputArray.Length > 1)
            return inputArray.Skip(1).ToArray();
        else
            return new string[0];
    }

    /// <summary>
    /// Returns the first element from a tokenized string, e.g.
    /// Input:"{tag}"  Output:"tag"
    /// </summary>
    /// <example>
    /// var clean = ExtractFirst("{tag}", '{', '}');
    /// </example>
    public static string ExtractFirst(this string text, char start, char end)
    {
        string pattern = @"\" + start + "(.*?)" + @"\" + end; //pattern = @"\{(.*?)\}"
        Match match = Regex.Match(text, pattern);
        if (match.Success)
            return match.Groups[1].Value;
        else
            return "";
    }

    /// <summary>
    /// Returns the last element from a tokenized string, e.g.
    /// Input:"{tag}"  Output:"tag"
    /// </summary>
    /// <example>
    /// var clean = ExtractLast("{tag}", '{', '}');
    /// </example>
    public static string ExtractLast(this string text, char start, char end)
    {
        string pattern = @"\" + start + @"(.*?)\" + end; //pattern = @"\{(.*?)\}"
        MatchCollection matches = Regex.Matches(text, pattern);
        if (matches.Count > 0)
        {
            Match lastMatch = matches[matches.Count - 1];
            return lastMatch.Groups[1].Value;
        }
        else
            return "";
    }

    /// <summary>
    /// Returns all the elements from a tokenized string, e.g.
    /// Input:"{tag}"  Output:"tag"
    /// </summary>
    public static string[] ExtractAll(this string text, char start, char end)
    {
        string pattern = @"\" + start + @"(.*?)\" + end; //pattern = @"\{(.*?)\}"
        MatchCollection matches = Regex.Matches(text, pattern);
        string[] results = new string[matches.Count];
        for (int i = 0; i < matches.Count; i++)
            results[i] = matches[i].Groups[1].Value;

        return results;
    }

    /// <summary>
    /// Returns the specified occurrence of a character in a string.
    /// </summary>
    /// <returns>
    /// Index of requested occurrence if successful, -1 otherwise.
    /// </returns>
    /// <example>
    /// If you wanted to find the second index of the percent character in a string:
    /// int index = "blah%blah%blah".IndexOfNth('%', 2);
    /// </example>
    public static int IndexOfNth(this string input, char character, int position)
    {
        int index = -1;

        if (string.IsNullOrEmpty(input))
            return index;

        for (int i = 0; i < position; i++)
        {
            index = input.IndexOf(character, index + 1);
            if (index == -1) { break; }
        }

        return index;
    }

    public static bool HasAlpha(this string str)
    {
        if (string.IsNullOrEmpty(str)) { return false; }
        return str.Any(x => char.IsLetter(x));
    }
    public static bool HasAlphaRegex(this string str)
    {
        return Regex.IsMatch(str ?? "", @"[+a-zA-Z]+");
    }

    public static bool HasNumeric(this string str)
    {
        if (string.IsNullOrEmpty(str)) { return false; }
        return str.Any(x => char.IsNumber(x));
    }
    public static bool HasNumericRegex(this string str)
    {
        return Regex.IsMatch(str ?? "", @"[0-9]+"); // [^\D+]
    }

    public static bool HasSpace(this string str)
    {
        if (string.IsNullOrEmpty(str)) { return false; }
        return str.Any(x => char.IsSeparator(x));
    }
    public static bool HasSpaceRegex(this string str)
    {
        return Regex.IsMatch(str ?? "", @"[\s]+");
    }

    public static bool HasPunctuation(this string str)
    {
        if (string.IsNullOrEmpty(str)) { return false; }
        return str.Any(x => char.IsPunctuation(x));
    }

    public static bool HasAlphaNumeric(this string str)
    {
        if (string.IsNullOrEmpty(str)) { return false; }
        return str.Any(x => char.IsNumber(x)) && str.Any(x => char.IsLetter(x));
    }
    public static bool HasAlphaNumericRegex(this string str)
    {
        return Regex.IsMatch(str ?? "", "[a-zA-Z0-9]+");
    }

    public static string RemoveAlphas(this string str)
    {
        return string.Concat(str?.Where(c => char.IsNumber(c) || c == '.') ?? string.Empty);
    }

    public static string RemoveNumerics(this string str)
    {
        return string.Concat(str?.Where(c => char.IsLetter(c)) ?? string.Empty);
    }

    public static string RemoveExtraSpaces(this string strText)
    {
        if (!string.IsNullOrEmpty(strText))
            strText = Regex.Replace(strText, @"\s+", " ");

        return strText;
    }

    public static Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> items)
    {
        if (items is null)
            throw new ArgumentNullException(nameof(items));

        return Implementation(items);

        static async Task<List<T>> Implementation(IAsyncEnumerable<T> items)
        {
            var rv = new List<T>();
            await foreach (var item in items)
            {
                rv.Add(item);
            }
            return rv;
        }
    }

    public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T>? toAdd)
    {
        if (collection is null)
            throw new ArgumentNullException(nameof(collection));

        if (toAdd != null)
        {
            foreach (var item in toAdd)
                collection.Add(item);
        }
    }

    public static void RemoveFirst<T>(this IList<T> collection, Func<T, bool> predicate)
    {
        if (collection is null)
            throw new ArgumentNullException(nameof(collection));

        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));

        for (int i = 0; i < collection.Count; i++)
        {
            if (predicate(collection[i]))
            {
                collection.RemoveAt(i);
                break;
            }
        }
    }

    /// <summary>
    /// Double group matching.
    /// </summary>
    public static List<string> ExtractWMICSubItems(this string text)
    {
        var items = new List<string>();
        var matches = Regex.Matches(text, "\"(.*?)\"|([^{},\\s]+)");

        foreach (Match match in matches)
        {
            if (match.Groups[1].Success)
                items.Add(match.Groups[1].Value);
            else if (match.Groups[2].Success)
                items.Add(match.Groups[2].Value);
        }

        return items;
    }

    /// <summary>
    /// Converts an object into a JSON string.
    /// </summary>
    /// <param name="item"></param>
    public static string ToJson(object item)
    {
        var ser = new DataContractJsonSerializer(item.GetType());
        using (var ms = new MemoryStream())
        {
            ser.WriteObject(ms, item);
            var sb = new StringBuilder();
            sb.Append(Encoding.UTF8.GetString(ms.ToArray()));
            return sb.ToString();
        }
    }

    /// <summary>
    /// Converts an IEnumerable collection into a JSON string.
    /// </summary>
    /// <param name="item"></param>
    public static string ToJson(IEnumerable collection, string rootName)
    {
        var ser = new DataContractJsonSerializer(collection.GetType());
        using (var ms = new MemoryStream())
        {
            ser.WriteObject(ms, collection);
            var sb = new StringBuilder();
            sb.Append("{ \"").Append(rootName).Append("\": ");
            sb.Append(Encoding.UTF8.GetString(ms.ToArray()));
            sb.Append(" }");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Converts a JSON string into the specified type.
    /// </summary>
    /// <typeparam name="T">the requested type</typeparam>
    /// <param name="jsonString">the JSON data</param>
    /// <returns>the requested type</returns>
    public static T? FromJsonTo<T>(string jsonString)
    {
        DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(T));
        using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(jsonString)))
        {
            T? jsonObject = (T?)ser.ReadObject(ms);
            return jsonObject;
        }
    }

    /// <summary>
    /// A long running garbage-friendly reference so we don't keep instantiating
    /// new Random() objects inside loops or other frequently used method calls.
    /// </summary>
    public static Random Rnd
    {
        get
        {
            var r = s_random.Target as Random;
            if (r == null)
                s_random.Target = r = new Random();

            return r;
        }
    }
    private static readonly WeakReference s_random = new WeakReference(null);

    public static async Task WaitAsync(this Task task)
    {
        try
        {
            await task;
        }
        catch (TaskCanceledException)
        {
            // Handle the cancellation (if needed).
            Console.WriteLine("Task was canceled.");

            // Re-throw the exception for further handling by caller.
            throw;
        }
    }

    /// <summary>
    /// In a console application, there is no synchronization context akin to UI contexts like in UWP, WPF
    /// or WinForms. Therefore, you should avoid using TaskScheduler.FromCurrentSynchronizationContext()
    /// in a console application. This change should allow the SafeFireAndForget method to run properly
    /// in a console application without attempting to use a synchronization context that doesn't exist
    /// in this context.
    /// </summary>
    public static void SafeFireAndForgetConsole(this Task task, Action<Exception>? errorHandler = null)
    {
        task.ContinueWith(t =>
        {   // Handle any exceptions if the task faulted.
            if (t.IsFaulted && errorHandler != null)
            {   // If we have an aggregate exception deal with it.
                if (t.Exception != null && t.Exception.InnerExceptions != null)
                {
                    foreach (var exception in t.Exception.InnerExceptions)
                    {
                        errorHandler(exception);
                    }
                }
            }
        }, TaskScheduler.Default);
    }

    /// <summary>
    /// In a console application, there is no synchronization context akin to UI contexts like in UWP, WPF
    /// or WinForms. Therefore, you should avoid using TaskScheduler.FromCurrentSynchronizationContext()
    /// in a console application. This method version should only be used in apps with UI thread context.
    /// </summary>
    public static void SafeFireAndForgetUWP(this Task task, Action<Exception>? errorHandler = null)
    {
        task.ContinueWith(t =>
        {   // Handle any exceptions if the task faulted
            if (t.IsFaulted && errorHandler != null)
            {   // If we have an aggregate exception deal with it.
                if (t.Exception != null && t.Exception.InnerExceptions != null)
                {
                    foreach (var exception in t.Exception.InnerExceptions)
                    {
                        errorHandler(exception);
                    }
                }
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>
    /// Task.Factory.StartNew (() => { throw null; }).IgnoreExceptions();
    /// </summary>
    public static void IgnoreExceptions(this Task task, Action<Exception>? errorHandler = null)
    {
        task.ContinueWith(t =>
        {
            AggregateException? ignore = t.Exception;

            ignore?.Flatten().Handle(ex =>
            {
                if (errorHandler != null)
                    errorHandler(ex);

                return true; // don't re-throw
            });

        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    /// <summary>
    /// Chainable task helper.
    /// var result = await SomeLongAsyncFunction().WithTimeout(TimeSpan.FromSeconds(2));
    /// </summary>
    /// <typeparam name="TResult">the type of task result</typeparam>
    /// <returns><see cref="Task"/>TResult</returns>
    public async static Task<TResult> WithTimeout<TResult>(this Task<TResult> task, TimeSpan timeout)
    {
        Task winner = await (Task.WhenAny(task, Task.Delay(timeout)));

        if (winner != task)
            throw new TimeoutException();

        return await task;   // Unwrap result/re-throw
    }

    /// <summary>
    /// Task extension to add a timeout.
    /// </summary>
    /// <returns>The task with timeout.</returns>
    /// <param name="task">Task.</param>
    /// <param name="timeoutInMilliseconds">Timeout duration in Milliseconds.</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    public async static Task<T> WithTimeout<T>(this Task<T> task, int timeoutInMilliseconds)
    {
        var retTask = await Task.WhenAny(task, Task.Delay(timeoutInMilliseconds)).ConfigureAwait(false);

        #pragma warning disable CS8603 // Possible null reference return.
        return retTask is Task<T> ? task.Result : default;
        #pragma warning restore CS8603 // Possible null reference return.
    }

    /// <summary>
    /// Chainable task helper.
    /// var result = await SomeLongAsyncFunction().WithCancellation(cts.Token);
    /// </summary>
    /// <typeparam name="TResult">the type of task result</typeparam>
    /// <returns><see cref="Task"/>TResult</returns>
    public static Task<TResult> WithCancellation<TResult>(this Task<TResult> task, CancellationToken cancelToken)
    {
        TaskCompletionSource<TResult> tcs = new TaskCompletionSource<TResult>();
        CancellationTokenRegistration reg = cancelToken.Register(() => tcs.TrySetCanceled());
        task.ContinueWith(ant =>
        {
            reg.Dispose(); // NOTE: it's important to dispose of CancellationTokenRegistrations or they will hand around in memory until the application closes
            if (ant.IsCanceled)
                tcs.TrySetCanceled();
            else if (ant.IsFaulted)
                tcs.TrySetException(ant.Exception?.InnerException ?? ant.Exception ?? new Exception("No exception information available."));
            else
                tcs.TrySetResult(ant.Result);
        });
        return tcs.Task;  // Return the TaskCompletionSource result
    }

    public static Task<T> WithAllExceptions<T>(this Task<T> task)
    {
        TaskCompletionSource<T> tcs = new TaskCompletionSource<T>();

        task.ContinueWith(ignored =>
        {
            switch (task.Status)
            {
                case TaskStatus.Canceled:
                    Console.WriteLine($"[TaskStatus.Canceled]");
                    tcs.SetCanceled();
                    break;
                case TaskStatus.RanToCompletion:
                    tcs.SetResult(task.Result);
                    //Console.WriteLine($"[TaskStatus.RanToCompletion({task.Result})]");
                    break;
                case TaskStatus.Faulted:
                    // SetException will automatically wrap the original AggregateException
                    // in another one. The new wrapper will be removed in TaskAwaiter, leaving
                    // the original intact.
                    Console.WriteLine($"[TaskStatus.Faulted]: {task.Exception?.Message}");
                    tcs.SetException(task.Exception ?? new Exception("No exception information available."));
                    break;
                default:
                    Console.WriteLine($"[TaskStatus: Continuation called illegally.]");
                    tcs.SetException(new InvalidOperationException("Continuation called illegally."));
                    break;
            }
        });

        return tcs.Task;
    }

    public static async Task RunTwoTasksParallel(Task task1, Task task2, Action<Exception>? errorHandler = null, bool configAwaitTask1 = false, bool configAwaitTask2 = false)
    {
        try
        {
            await Task.WhenAll(configAwaitTask1 ? task1 : task1.AwaitAsTask(), configAwaitTask2 ? task2 : task2.AwaitAsTask());
        }
        catch (Exception ex)
        {
            errorHandler?.Invoke(ex);
        }

    }
    static async Task AwaitAsTask(this Task task)
    {
        await task.ConfigureAwait(false);
    }

    public static async Task RunTwoTasksSequential(Task task1, Task task2, Action<Exception>? errorHandler = null, bool configAwaitTask1 = false, bool configAwaitTask2 = false)
    {
        try
        {
            await task1.ConfigureAwait(configAwaitTask1);
            await task2.ConfigureAwait(configAwaitTask2);
        }
        catch (Exception ex)
        {
            errorHandler?.Invoke(ex);
        }
    }

    public static async Task RunAllTasksSequential(this Task[] tasks, Action<Exception>? errorHandler = null, bool configAwaitTask = false)
    {
        foreach (var task in tasks)
        {
            try
            {
                await task.ConfigureAwait(configAwaitTask);
            }
            catch (Exception ex)
            {
                errorHandler?.Invoke(ex);
            }
        }
    }

    public static async Task RunAllTasksSequential(this List<Task> tasks, Action<Exception>? errorHandler = null, bool configAwaitTask = false)
    {
        foreach (var task in tasks)
        {
            try
            {
                await task.ConfigureAwait(configAwaitTask);
            }
            catch (Exception ex)
            {
                errorHandler?.Invoke(ex);
            }
        }
    }

    public static async Task RunAllTasksParallel(this Task[] tasks, Action<Exception>? errorHandler = null, bool configAwaitTask = false)
    {
        try
        {
            await Task.WhenAll(Array.ConvertAll(tasks, task => configAwaitTask ? task.AwaitAsTask() : task)).ConfigureAwait(configAwaitTask);
        }
        catch (Exception ex)
        {
            errorHandler?.Invoke(ex);
        }
    }

    public static async Task RunTasksInParallel(this List<Task> tasks, Action<Exception>? errorHandler = null, bool configAwaitTask = false)
    {
        try
        {
            await Task.WhenAll(tasks.ConvertAll(task => configAwaitTask ? task.AwaitAsTask() : task)).ConfigureAwait(configAwaitTask);
        }
        catch (Exception ex)
        {
            errorHandler?.Invoke(ex);
        }
    }

    /// <summary>
    /// The <see cref="BlockingCollection{T}"/> does not offer a way to clear items without removing them.
    /// </summary>
    /// <typeparam name="T">the type</typeparam>
    /// <param name="blockingCollection"><see cref="BlockingCollection{T}"/></param>
    /// <exception cref="ArgumentNullException"></exception>
    public static void Clear<T>(this BlockingCollection<T> blockingCollection)
    {
        if (blockingCollection == null)
            throw new ArgumentNullException(nameof(blockingCollection));

        while (blockingCollection.Count > 0)
            blockingCollection.TryTake(out T? item);
    }

    /// <summary>
    /// A generic helper method that takes a variable number of arguments (params T[] items)
    /// and returns a List<T>. The params keyword allows you to pass any number of arguments
    /// to the method.
    /// </summary>
    /// <typeparam name="T">the type</typeparam>
    /// <param name="items">array of items</param>
    /// <returns><see cref="List{T}"/></returns>
    public static List<T> CreateList<T>(params T[] items)
    {
        return new List<T>(items);
    }

    /// <summary>
    /// Converts a <see cref="List{T}"/> into a <see cref="BlockingCollection{T}"/>.
    /// </summary>
    public static BlockingCollection<T> ToBlockingCollection<T>(this List<T> list)
    {
        var blockCollection = new BlockingCollection<T>();

        if (list == null)
            return blockCollection;

        foreach (var item in list)
            blockCollection.Add(item);

        return blockCollection;
    }

    /// <summary>
    /// Converts a <see cref="BlockingCollection{T}"/> into a <see cref="List{T}"/>.
    /// </summary>
    public static List<T> ToList<T>(this BlockingCollection<T> blockCollection)
    {
        var list = new List<T>();

        if (blockCollection == null)
            return list;

        while (blockCollection.TryTake(out T? item))
            list.Add(item);

        return list;
    }

    /// <summary>
    /// A generic helper method that takes an array and returns a new array containing the last 
    /// two elements. The Skip method is used to skip all but the last <paramref name="count"/>
    /// elements of the array.
    /// </summary>
    /// <typeparam name="T">the type</typeparam>
    /// <param name="count">number of elements</param>
    /// <param name="items">array of items</param>
    /// <param name="reverse">true to reverse the elements, false otherwise</param>
    /// <returns>last two elements</returns>
    /// <exception cref="ArgumentException">if array contains less than n items</exception>
    public static T[] GetLast<T>(this T[] items, int count = 2, bool reverse = false)
    {
        if (items.Length < count)
            return new T[0]; // throw new ArgumentException($"Array must contain at least {count} elements.");

        if (reverse)
            return items.Skip(items.Length - count).Reverse().ToArray();

        return items.Skip(items.Length - count).ToArray();
    }

    /// <summary>
    /// A generic helper method that takes an array and returns a new array containing the last 
    /// two elements. The Skip method is used to skip all but the last <paramref name="count"/>
    /// elements of the array.
    /// </summary>
    /// <typeparam name="T">the type</typeparam>
    /// <param name="items"><see cref="IEnumerable{T}"/></param>
    /// <param name="count">number of elements</param>
    /// <param name="reverse">true to reverse the elements, false otherwise</param>
    /// <returns>last two elements</returns>
    /// <exception cref="ArgumentException">if array contains less than 2 items</exception>
    public static IEnumerable<T> GetLast<T>(this IEnumerable<T> items, int count = 2, bool reverse = false)
    {
        if (items.Count() < count)
            return Enumerable.Empty<T>(); //throw new ArgumentException($"The collection must contain at least {count} elements.");

        if (reverse)
            return items.Skip(items.Count() - count).Reverse();

        return items.Skip(items.Count() - count);
    }

    public static string GetLast(this string text, int numChars)
    {
        if (numChars >= text.Length)
            return text;

        return text.Substring(text.Length - numChars);
    }

    /// <summary>
    /// <br>Converts run-on string of dollar values into list of <see cref="decimal"/> values.</br>
    /// <br>var prices = GetDecimalValues("PLU10.00$5.00210.00446");</br>
    /// </summary>
    /// <returns><see cref="List{T}"/></returns>
    public static List<decimal> GetDecimalValues(string input)
    {
        List<decimal> decimalValues = new List<decimal>();

			if (string.IsNullOrEmpty(input))
				return decimalValues;

        string clean = Regex.Replace(input, "[A-Za-z]", "");
        MatchCollection matches = Regex.Matches(clean, @"\d+\.\d{2}");
        foreach (Match match in matches)
        {
            if (decimal.TryParse(match.Value, out decimal value))
            {
                decimalValues.Add(value);
            }
        }
        return decimalValues;
    }

    public static string AddOrdinalSuffix(this int num)
    {
        if (num <= 0)
            return $"{num}";

        switch (num % 100)
        {
            case 11:
            case 12:
            case 13: return $"{num}th";
        }

        switch (num % 10)
        {
            case 1: return $"{num}st";
            case 2: return $"{num}nd";
            case 3: return $"{num}rd";
            default: return $"{num}th";
        }
    }

    /// <summary>
    /// Parses the <paramref name="xml"/> and returns a <see cref="Dictionary{TKey, TValue}"/>
    /// which contains the keys as the elements and the values as the content.
    /// </summary>
	/// <example>
    /// <code>
    /// var dict = configFileXmlData.GetValuesFromXml("add");
    /// foreach (var cfg in dict) 
    ///    Console.WriteLine($"{cfg.Key} ⇨ {cfg.Value}");
	/// </code>
	/// </example>
    /// <exception cref="System.Exception">
    /// Thrown when one parameter is
    /// <see cref="Int32.MaxValue">MaxValue</see> and the other is
    /// greater than 0.
    /// Note that here you can also use
    /// <see href="https://learn.microsoft.com/dotnet/api/system.int32.maxvalue"/>
    ///  to point a web page instead.
    /// </exception>
	/// <param name="xml">The raw XML string.</param>
	/// <param name="element">The element name to look for.</param>
	/// <returns><see cref="Dictionary{TKey, TValue}"/></returns>
	public static Dictionary<string, string> GetValuesFromXml(this string xml, string element = "add")
    {
        Dictionary<string, string> dict = new Dictionary<string, string>();

		try
		{
			XElement root = XElement.Parse(xml);
			foreach (XElement xe in root.Descendants(element))
			{
                // Check for empty elements, e.g. "<add></add>"
                if (xe.Attribute("key") != null && xe.Attribute("value") != null)
				{
					    string key = xe.Attribute("key").Value;
					    string value = xe.Attribute("value").Value;
					    dict[key] = value;
			    }
			}
		}
		catch (Exception ex)
		{
            Console.WriteLine($"GetValuesFromXml: {ex.Message}");
        }

        return dict;
    }

    /// <summary>
    /// Checks if there are any elements with the specified <paramref name="xml"/>
    /// among the root element and its descendants. If there are, the method 
    /// returns true; otherwise, it returns false.
    /// </summary>
    /// <param name="xml">The xml data.</param>
    /// <param name="elementName">The node name to validate.</param>
    /// <returns>true if exists, false otherwise</returns>
    public static bool DoesDescendantExist(this string xml, string elementName)
    {
        try
        {
            // Check if passed "<name>" instead of "name".
            elementName = RemoveXmlTags(elementName);

            XElement root = XElement.Parse(xml);
            return root.DescendantsAndSelf(elementName).Any();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DoesDescendantExist: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// NOTE: This will not check descendants of the node.
    /// Checks for child elements of the root element matching <paramref name="elementName"/>.
    /// Will also check if <paramref name="elementName"/> matches the root element itself.
    /// </summary>
    /// <param name="xml">The xml data.</param>
    /// <param name="elementName">The node name to validate.</param>
    /// <returns>true if exists, false otherwise</returns>
    //public static bool DoesElementExist(this string xml, string elementName)
    //{
    //	try
    //	{
    //		elementName = RemoveXmlTags(elementName);
    //		XElement root = XElement.Parse(xml);
    //		return root.Name == elementName || root.Element(elementName) != null;
    //	}
    //    catch (Exception)
    //    {
    //        return false;
    //    }
    //}

    /// <summary>
    /// Retuns the node count from the provided <paramref name="xml"/>.
    /// </summary>
    public static int CountElementsInXml(this string xml)
    {
        try
        {
            XElement root = XElement.Parse(xml);
            return root.DescendantsAndSelf().Count();
        }
        catch (Exception)
        {
            return -1;
        }
    }

    public static List<String> EnumToList(Type sType)
    {
        List<String> aList = new List<string>();
        foreach (System.Reflection.FieldInfo fInfo in sType.GetFields())
        {
            if (fInfo.FieldType == sType)
            {
                Console.WriteLine(fInfo.Name);
                Console.WriteLine(fInfo.GetRawConstantValue().ToString());
                aList.Add(fInfo.Name);
            }
        }
        return aList;
    }

    /// <summary>
    /// Determine if type contains the default value.
    /// </summary>
    public static bool IsDefaultValue<T>(T value) => object.Equals(default(T), value);

    /// <summary>
    /// Exchange two arguments passed by address.
    /// </summary>
    public static void Swap<T>(ref T x, ref T y)
    {
        T tmp = x;
        x = y;
        y = tmp;
    }

    public static string RemoveMultipleSpacesToSingle(string strText)
    {
        if (!string.IsNullOrEmpty(strText))
            strText = System.Text.RegularExpressions.Regex.Replace(strText, @"\s+", " ");

        return strText;
    }

    public static void SetCulture(string language)
    {
        if (string.IsNullOrEmpty(language))
            language = System.Threading.Thread.CurrentThread.CurrentCulture.Name;

        try
        {
            System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(language);
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.CreateSpecificCulture(language);
        }
        catch (System.Globalization.CultureNotFoundException ex)
        {
            throw new Exception($"Culture '{language}' was not found.", ex);
        }
    }

    public static bool IsValidCluture(string cultureName)
    {
        bool retVal = true;
        if (!string.IsNullOrEmpty(cultureName))
        {
            System.Globalization.CultureInfo[] cinfo = System.Globalization.CultureInfo.GetCultures(System.Globalization.CultureTypes.NeutralCultures);
            //System.Globalization.CultureInfo.GetCultures(System.Globalization.CultureTypes.AllCultures & ~System.Globalization.CultureTypes.NeutralCultures);
            //List<string> ciList = (from i in cinfo where i.Name.Length == 2 orderby i.TwoLetterISOLanguageName select i.TwoLetterISOLanguageName).ToList();
            //ciList.Insert(0, "");
            if (cinfo.Where(i => i.TwoLetterISOLanguageName == cultureName).Count() < 1)
                retVal = false;
        }
        return retVal;
    }

    public static string GetCulture() => System.Threading.Thread.CurrentThread.CurrentCulture.Name;
    public static string GetDateFormat() => System.Threading.Thread.CurrentThread.CurrentCulture.DateTimeFormat.ShortDatePattern;
    public static string GetTimeFormat()
    {
        string timeFormat = System.Threading.Thread.CurrentThread.CurrentCulture.DateTimeFormat.ShortTimePattern;
        timeFormat = timeFormat.Replace("tt", "a");
        return timeFormat;
    }

    /// <summary>
    /// Uses the <see cref="System.Reflection.Module"/> contained in the
		/// <see cref="System.Reflection.Assembly"/> to determine the current path.
    /// </summary>
    /// <returns>current assembly's path</returns>
    public static string GetPathFromAssemblyModules()
    {
			string path = string.Empty;
        var mods = Assembly.GetExecutingAssembly().GetLoadedModules();
        Console.WriteLine($"Assembly is comprised of the following modules:");
        foreach (var mod in mods)
        {
            Console.WriteLine($" • {mod.FullyQualifiedName}");
				try { path = Path.GetDirectoryName($"{mod.FullyQualifiedName}"); }
				catch (Exception) { }
        }
			return path;
    }

    public static void Announcement(this string msg)
    {
        Console.BackgroundColor = ConsoleColor.Black;
        var tmp = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine(msg);
        Console.ForegroundColor = tmp;
    }

    /// <summary>
    /// Basic key/pswd generator for unique IDs.
    /// This employs the standard MS key table which accounts
    /// for the 36 Latin letters and Arabic numerals used in
    /// most Western European languages...
    /// 24 chars are favored: 2346789 BCDFGHJKMPQRTVWXY
    /// 12 chars are avoided: 015 AEIOU LNSZ
    /// Only 2 chars are occasionally mistaken: 8 and B (depends on the font).
    /// The base of possible codes is large (about 3.2 * 10^34).
    /// </summary>
    public static string GetRandomKey(int pLength = 6)
    {
        const string pwChars = "2346789BCDFGHJKMPQRTVWXY";
        if (pLength < 6)
            pLength = 6; // minimum of 6 characters

        char[] charArray = pwChars.Distinct().ToArray();
        var result = new char[pLength];
        for (int x = 0; x < pLength; x++)
            result[x] = pwChars[Random.Shared.Next() % pwChars.Length];

        return (new string(result));
    }

    /// <summary>
    /// For generating data objects.
    /// </summary>
    /// <returns>a randomly selected first name</returns>
    public static string GetRandomName()
    {
        string[] names = {
            "Aaliyah",
            "Aaron",
            "Abigail",
            "Adam",
            "Addison",
            "Adrian",
            "Aiden",
            "Alan",
            "Albert",
            "Alex",
            "Alexander",
            "Alexis",
            "Alice",
            "Amanda",
            "Amara",
            "Amber",
            "Amelia",
            "Amir",
            "Amy",
            "Andrea",
            "Andrew",
            "Angela",
            "Ann",
            "Anna",
            "Anthony",
            "Aria",
            "Ariana",
            "Arthur",
            "Arya",
            "Asher",
            "Ashley",
            "Athena",
            "Aubrey",
            "Audrey",
            "Aurora",
            "Austin",
            "Autumn",
            "Ava",
            "Avery",
            "Ayla",
            "Barbara",
            "Bart",
            "Beau",
            "Bella",
            "Benjamin",
            "Betty",
            "Beverly",
            "Billy",
            "Bobby",
            "Bradley",
            "Brandon",
            "Brenda",
            "Brian",
            "Brielle",
            "Brittany",
            "Brooklyn",
            "Bruce",
            "Bryan",
            "Caleb",
            "Cameron",
            "Camila",
            "Carl",
            "Carol",
            "Carolyn",
            "Carson",
            "Carter",
            "Catherine",
            "Charles",
            "Charlie",
            "Charlotte",
            "Cheryl",
            "Chloe",
            "Christian",
            "Christina",
            "Christine",
            "Christopher",
            "Claire",
            "Colton",
            "Cooper",
            "Cynthia",
            "Daniel",
            "Danielle",
            "David",
            "Deborah",
            "Debra",
            "Declan",
            "Delilah",
            "Denise",
            "Dennis",
            "Diana",
            "Diane",
            "Dominic",
            "Donald",
            "Donna",
            "Doris",
            "Dorothy",
            "Douglas",
            "Dylan",
            "Easton",
            "Edward",
            "Eleanor",
            "Elena",
            "Eli",
            "Eliana",
            "Elias",
            "Elijah",
            "Elizabeth",
            "Ella",
            "Ellie",
            "Emery",
            "Emilia",
            "Emily",
            "Emma",
            "Eric",
            "Ethan",
            "Eugene",
            "Eva",
            "Evelyn",
            "Everett",
            "Everleigh",
            "Everly",
            "Ezekiel",
            "Ezra",
            "Frances",
            "Frank",
            "Gabriel",
            "Gabriella",
            "Gary",
            "George",
            "Gerald",
            "Gianna",
            "Gloria",
            "Grace",
            "Grayson",
            "Gregory",
            "Greyson",
            "Hailey",
            "Hannah",
            "Harold",
            "Harper",
            "Hazel",
            "Heather",
            "Helen",
            "Henry",
            "Hudson",
            "Hunter",
            "Iris",
            "Isaac",
            "Isabella",
            "Isaiah",
            "Isla",
            "Ivy",
            "Jace",
            "Jack",
            "Jackson",
            "Jacob",
            "Jacqueline",
            "Jade",
            "James",
            "Jameson",
            "Janet",
            "Janice",
            "Jason",
            "Jaxon",
            "Jaxson",
            "Jayden",
            "Jean",
            "Jeffrey",
            "Jennifer",
            "Jeremiah",
            "Jeremy",
            "Jerry",
            "Jesse",
            "Jessica",
            "Joan",
            "Joe",
            "John",
            "Jonathan",
            "Jordan",
            "Jose",
            "Joseph",
            "Joshua",
            "Josiah",
            "Joyce",
            "Juan",
            "Judith",
            "Judy",
            "Julia",
            "Julian",
            "Julie",
            "Justin",
            "Kai",
            "Karen",
            "Katherine",
            "Kathleen",
            "Kathryn",
            "Kayden",
            "Kayla",
            "Keith",
            "Kelly",
            "Kennedy",
            "Kenneth",
            "Kevin",
            "Kimberly",
            "Kingston",
            "Kinsley",
            "Kyle",
            "Landon",
            "Larry",
            "Laura",
            "Lauren",
            "Lawrence",
            "Layla",
            "Leah",
            "Leilani",
            "Leo",
            "Levi",
            "Liam",
            "Lillian",
            "Lily",
            "Lincoln",
            "Linda",
            "Lisa",
            "Logan",
            "Lori",
            "Luca",
            "Lucas",
            "Lucy",
            "Luke",
            "Luna",
            "Madelyn",
            "Madison",
            "Margaret",
            "Maria",
            "Marie",
            "Marilyn",
            "Mark",
            "Martha",
            "Mary",
            "Mason",
            "Mateo",
            "Matthew",
            "Maverick",
            "Maya",
            "Megan",
            "Melissa",
            "Melody",
            "Mia",
            "Micah",
            "Michael",
            "Michelle",
            "Mila",
            "Miles",
            "Muhammad",
            "Nancy",
            "Naomi",
            "Natalia",
            "Natalie",
            "Nathan",
            "Nevaeh",
            "Nicholas",
            "Nicole",
            "Noah",
            "Nolan",
            "Nora",
            "Nova",
            "Oliver",
            "Olivia",
            "Owen",
            "Paisley",
            "Pamela",
            "Patricia",
            "Patrick",
            "Paul",
            "Paula",
            "Penelope",
            "Peter",
            "Philip",
            "Piper",
            "Quinn",
            "Rachel",
            "Raelynn",
            "Ralph",
            "Randy",
            "Raymond",
            "Rebecca",
            "Richard",
            "Riley",
            "Robert",
            "Roger",
            "Roman",
            "Ronald",
            "Rowan",
            "Roy",
            "Ruby",
            "Russell",
            "Ruth",
            "Ryan",
            "Ryder",
            "Rylee",
            "Ryleigh",
            "Sadie",
            "Samantha",
            "Samuel",
            "Sandra",
            "Santiago",
            "Sara",
            "Sarah",
            "Savannah",
            "Scarlett",
            "Scott",
            "Sean",
            "Sebastian",
            "Serenity",
            "Sharon",
            "Shirley",
            "Silas",
            "Skylar",
            "Sofia",
            "Sophia",
            "Sophie",
            "Stella",
            "Stephanie",
            "Stephen",
            "Steven",
            "Susan",
            "Teresa",
            "Terry",
            "Theo",
            "Theodore",
            "Theresa",
            "Thomas",
            "Timothy",
            "Tyler",
            "Valentina",
            "Victoria",
            "Vincent",
            "Violet",
            "Virginia",
            "Walter",
            "Waylon",
            "Wayne",
            "Weston",
            "William",
            "Willie",
            "Willow",
            "Wyatt",
            "Xavier",
            "Zachary",
            "Zoe",
            "Zoey",};
        string retVal = names[Random.Shared.Next(names.Length)];
        return retVal;
    }

    #region [XML Helpers]
    /// <summary>
    /// Extracts the XML node name and removes all delimiters.
    /// </summary>
    public static string RemoveXmlTags(this string input)
	{
		// Closing tag.
		System.Text.RegularExpressions.Match attempt1 = System.Text.RegularExpressions.Regex.Match(input, "<(.*?)/>");
		if (attempt1.Success)
			return attempt1.Groups[1].Value.Trim();

		// Opening tag.
		System.Text.RegularExpressions.Match attempt2 = System.Text.RegularExpressions.Regex.Match(input, "<(.*?)>");
		if (attempt2.Success)
			return attempt2.Groups[1].Value.Trim();

		return input;
	}

    /// <summary>
    /// Can be helpful with XML payloads that contain too many namespaces.
    /// </summary>
    /// <param name="xmlDocument"></param>
    /// <param name="disableFormatting"></param>
    /// <returns>sanitized XML</returns>
    public static string RemoveAllNamespaces(string xmlDocument, bool disableFormatting = true)
    {
        try
        {
            XElement xmlDocumentWithoutNs = RemoveAllNamespaces(XElement.Parse(xmlDocument));
            if (disableFormatting)
                return xmlDocumentWithoutNs.ToString(SaveOptions.DisableFormatting);
            else
                return xmlDocumentWithoutNs.ToString();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"RemoveAllNamespaces: {ex.Message}");
            return xmlDocument;
        }
    }
    static XElement RemoveAllNamespaces(XElement? e)
    {
        return new XElement(e?.Name.LocalName,
                            (from n in e?.Nodes()
                            select ((n is XElement) ? RemoveAllNamespaces(n as XElement) : n)),
                            (e != null && e.HasAttributes) ?
                            (from a in e?.Attributes()
                            where (!a.IsNamespaceDeclaration)
                            select new XAttribute(a.Name.LocalName, a.Value)) : null);
    }

    /// <summary>
    /// Extracts a single <see cref="XmlNode"/> and returns the OuterXml.
    /// </summary>
		/// <example>
    /// <code>
    /// var inner = ExtractSingleNodeFromXML(xmlResponse, "plu");
    /// XmlSerializer serializer = new XmlSerializer(typeof(PluWithOriginalPrice));
    /// using (StringReader reader = new StringReader(inner))
    /// {
    ///    var result = (PluWithOriginalPrice)serializer.Deserialize(reader);
    /// }
    /// </code>
		/// </example>
    /// <param name="xml">XML data response.</param>
    /// <param name="name">The inner element node to extract.</param>
    /// <returns>All elements contained within <paramref name="name"/>, else an empty string.</returns>
    public static string ExtractSingleElementNode(this string xml, string name)
    {
        string result = string.Empty;

        try
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            XmlNode pluNode = doc.SelectSingleNode($"//{name}");
            if (pluNode != null)
            {
                result = pluNode.OuterXml;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ExtractSingleElementNode: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// A recursive node reader.
    /// </summary>
	/// <example>
	/// <code>
    /// while (xmlReader.Read())
	/// {
    ///    if (xmlReader.NodeType == XmlNodeType.Element)
	///    { 
	///       xmlReader.TraverseNode(); 
	///    }
	/// }
	/// </code>
	/// </example>
    /// <param name="reader"><see cref="XmlReader"/></param>
    /// <param name="name">current node name (optional)</param>
    public static void TraverseNode(this XmlReader reader, string name = "")
    {
        // If the reader is positioned on an element
        if (reader.NodeType == XmlNodeType.Element)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("─────────────────────────────────────────────────────────────────────");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"ElementName...: {reader.Name}");
            Console.WriteLine($"ElementDepth..: {reader.Depth}");
            // If the element has attributes, print them
            if (reader.HasAttributes)
            {
                Console.WriteLine("[Attributes]");
                while (reader.MoveToNextAttribute())
                {
                    Console.WriteLine($" • {reader.Name}: {reader.Value}");
                }
                // Move back to the element node.
                reader.MoveToElement();
            }
            // If the element is empty, return.
            if (reader.IsEmptyElement)
            {
                Console.WriteLine();
                return;
            }
        }

        // Read the next node
        while (reader.Read())
        {
            // If the node is an end element, we're done.
            if (reader.NodeType == XmlNodeType.EndElement)
            {
                return;
            }
            // If the node is an element, call ourself (recursive).
            if (reader.NodeType == XmlNodeType.Element)
            {
                TraverseNode(reader, reader.Name);
            }
            // If the node is a text node, show the content.
            else if (reader.NodeType == XmlNodeType.Text)
            {
                Console.WriteLine($"{(!string.IsNullOrEmpty(name) ? $"<{name}> " : "")}Text: {reader.Value}");
            }
        }
    }

    /// <summary>
    /// Creates a dictionary using the element name as the key and the node's contents as the values.
    /// </summary>
    /// <param name="xml">The XML string to parse.</param>
    /// <param name="dump">If true, the contents will be output to the console.</param>
    /// <returns><see cref="Dictionary{string, List{string}}"/></returns>
    public static Dictionary<string, List<string>> ConvertXmlIntoDictionary(this string xml, bool dump = false)
    {
        Dictionary<string, List<string>> dict = new Dictionary<string, List<string>>();

        try
        {
            XElement root = XElement.Parse(xml);

            foreach (XElement element in root.DescendantsAndSelf())
            {
                if (!dict.ContainsKey(element.Name.LocalName))
                    dict[element.Name.LocalName] = new List<string>();

                if (!string.IsNullOrEmpty(element.Value.Trim()))
                    dict[element.Name.LocalName].Add(element.Value.Trim());

                foreach (XAttribute attribute in element.Attributes())
                {
                    if (!dict.ContainsKey(attribute.Name.LocalName))
                        dict[attribute.Name.LocalName] = new List<string>();

                    dict[attribute.Name.LocalName].Add(attribute.Value);
                }
            }

            if (dump)
            {
                foreach (var pair in dict)
                {
                    Console.WriteLine($"Key ⇨ {pair.Key}");
                    Console.WriteLine($" • {string.Join(", ", pair.Value)}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ConvertXmlIntoDictionary: {ex.Message}");
        }

        return dict;
    }


    /// <summary>
    /// Convert object into XML data using <see cref="XmlSerializer"/>.
    /// </summary>
    /// <param name="obj">object to serialize</param>
    /// <param name="enc">the <see cref="Encoding"/> to use, if null UTF8 is chosen</param>
    /// <returns>XML string</returns>
    /// <remarks>
    /// If the object to serialize involes types other than primitives, e.g. <see cref="Dictionary{TKey, TValue}"/>,
    /// then the <see cref="XmlSerializer"/> will need to be contructed with the extraTypes.
    /// </remarks>
    public static string GetXmlFromObject(object obj, Encoding enc = null)
		{
			if (enc == null)
				enc = Encoding.UTF8;

			StringWriterWithEncoding sw = new StringWriterWithEncoding(enc);
			XmlTextWriter tw = null;

			try
			{
				XmlSerializer serializer = new XmlSerializer(obj.GetType());
				tw = new XmlTextWriter(sw);
				serializer.Serialize(tw, obj);
			}
			catch (InvalidOperationException ioex)
			{
				throw new Exception("Failed to serialize: " + ioex.Message +
									Environment.NewLine +
									"InnerException: " +
									ioex.InnerException?.Message);
			}
			catch (Exception ex)
			{
				throw new Exception("Failed to serialize: " + ex.Message +
									Environment.NewLine +
									"InnerException: " +
									ex.InnerException?.Message);
			}
			finally
			{
				sw.Close();

				if (tw != null)
					tw.Close();
			}

			return sw.ToString();
		}

		/// <summary>
		/// Serializes an object to XML format.
		/// </summary>
		/// <param name="xmlobj"></param>
		/// <returns>XML string</returns>
		/// <remarks>
		/// If the object to serialize involes types other than primitives, e.g. <see cref="Dictionary{TKey, TValue}"/>,
		/// then the <see cref="XmlSerializer"/> will need to be contructed with the extraTypes.
		/// </remarks>
		public static string SerializeToXML(object xmlobj)
		{
			string rt = string.Empty;
			try
			{
				using (var ms = new MemoryStream())
				{
					using (var tw = new XmlTextWriter(ms, new UTF8Encoding(true)) { Formatting = Formatting.Indented })
					{
						XmlSerializer serializer = new XmlSerializer(xmlobj.GetType());
						serializer.Serialize(tw, xmlobj);
						rt = Encoding.UTF8.GetString(ms.ToArray()).Replace("encoding=\"utf-8\"", string.Empty);
					}
				}
			}
			catch (Exception ex)
			{
				throw new Exception("Failed to serialize: " + ex.Message +
					Environment.NewLine +
					"InnerException: " +
					ex.InnerException?.Message);
			}

			return rt;
		}

		/// <summary>
		/// Serializes an object to an XElement.
		/// </summary>
		/// <typeparam name="T">The type of the object to serialize.</typeparam>
		/// <param name="xmlobj">The object to serialize.</param>
		/// <returns>The XElement or null if an error occurs.</returns>
		/// <remarks>
		/// If the object to serialize involes types other than primitives, e.g. <see cref="Dictionary{TKey, TValue}"/>,
		/// then the <see cref="XmlSerializer"/> will need to be contructed with the extraTypes.
		/// </remarks>
		public static XElement SerializeToXElement<T>(T xmlobj)
		{
			try
			{
				using (MemoryStream memoryStream = new MemoryStream())
				{
					using (TextWriter streamWriter = new StreamWriter(memoryStream))
					{
						XmlSerializer xmlSerializer = new XmlSerializer(typeof(T));
						xmlSerializer.Serialize(streamWriter, xmlobj);
						return XElement.Parse(Encoding.UTF8.GetString(memoryStream.ToArray()));
					}
				}
			}
			catch (Exception ex)
			{
				throw new Exception("Failed to serialize: " + ex.Message +
					Environment.NewLine +
					"InnerException: " +
					ex.InnerException?.Message);
			}
		}

		/// <summary>
		/// Create object from XML string using <see cref="XmlSerializer"/>.
		/// </summary>
		/// <param name="xml">xml string data</param>
		/// <param name="objectType">type of object to serialize</param>
		/// <returns></returns>
		/// <remarks>
		/// If the object to serialize involes types other than primitives, e.g. <see cref="Dictionary{TKey, TValue}"/>,
		/// then the <see cref="XmlSerializer"/> will need to be contructed with the extraTypes.
		/// </remarks>
		public static new Object GetObjectFromXml(string xml, Type objectType)
		{
			Object obj = null;

			try
			{
				using (StringReader strReader = new StringReader(xml))
				{
					XmlSerializer serializer = new XmlSerializer(objectType);
					using (XmlTextReader xmlReader = new XmlTextReader(strReader))
					{
                    xmlReader.Settings.IgnoreComments = true;
                    xmlReader.Settings.IgnoreWhitespace = true;
                    xmlReader.Settings.ConformanceLevel = ConformanceLevel.Auto;
						xmlReader.WhitespaceHandling = WhitespaceHandling.All;
						xmlReader.Namespaces = true; // If namespaces are present in the xml this is very important!
                    obj = serializer.Deserialize(xmlReader);
					}
				}
			}
			catch (InvalidOperationException ioex)
			{
				throw new Exception("Failed to deserialize: " +
									xml +
									Environment.NewLine +
									ioex.Message +
									Environment.NewLine +
									"InnerException: " + ioex.InnerException?.Message);
			}
			catch (Exception ex)
			{
				throw new Exception("Failed to deserialize: " +
									xml +
									Environment.NewLine +
									ex.Message +
									Environment.NewLine +
									"InnerException: " + ex.InnerException?.Message);
			}

			return obj;
		}

		public static string PrintXML(string xml)
		{
			Encoding enc = Encoding.ASCII;
			if (xml.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\""))
				enc = Encoding.UTF8;

			string result = "";
			MemoryStream mStream = new MemoryStream();
			XmlTextWriter writer = new XmlTextWriter(mStream, enc);
			try
			{
				XmlDocument document = new XmlDocument();
				// Load the XmlDocument with the XML.
				document.LoadXml(xml);
				writer.Formatting = Formatting.Indented;
				writer.Indentation = 3;
				// Write the XML into a formatting XmlTextWriter
				document.WriteContentTo(writer);
				writer.Flush();
				mStream.Flush();
				// Have to rewind the MemoryStream in order to read its contents.
				mStream.Position = 0;
				// Read MemoryStream contents into a StreamReader.
				StreamReader sReader = new StreamReader(mStream);
				// Extract the text from the StreamReader.
				string formattedXml = sReader.ReadToEnd();
				result = formattedXml;
			}
			catch (XmlException ex)
			{
				Console.WriteLine($"PrintXml(ERROR): {ex.Message}");
			}
			finally
			{
				mStream.Close();
				writer.Close();
			}

			return result;
		}

		public static string PrettyXml(string xml)
		{
			try
			{
				var stringBuilder = new StringBuilder();
				var element = System.Xml.Linq.XElement.Parse(xml);
				var settings = new XmlWriterSettings();
				settings.OmitXmlDeclaration = true;
				settings.Indent = true;
				settings.NewLineOnAttributes = true;
				// XmlWriter offers a StringBuilder as an output.
				using (var xmlWriter = XmlWriter.Create(stringBuilder, settings))
				{
					element.Save(xmlWriter);
				}

				return stringBuilder.ToString();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"PrettyXml: {ex.Message}");
				return string.Empty;
			}
		}
    #endregion [XML Helpers]

    /// <summary>
    /// A more verbose method that I have reworked.
    /// IMPORTANT NOTE: If your executable assembly manifest doesn't explicitly state that your
    /// exe assembly is compatible with Windows 8.1 or Windows 10.0, System.Environment.OSVersion
    /// will return Windows 8 version, which is 6.2, instead of 6.3 and 10.0!
    /// </summary>
    /// <returns>formatted OS string</returns>
    public static string GetOSDescription()
    {
        //Get Operating system information.
        OperatingSystem os = Environment.OSVersion;
        //Get version information about the os.
        Version vs = os.Version;

        //Variable to hold our return value
        string operatingSystem = "";

        if (os.Platform == PlatformID.Win32Windows)
        {
            //This is a pre-NT version of Windows
            switch (vs.Minor)
            {
                case 0:
                    operatingSystem = "95";
                    break;
                case 10:
                    if (vs.Revision.ToString() == "2222A")
                        operatingSystem = "98SE";
                    else
                        operatingSystem = "98";
                    break;
                case 90:
                    operatingSystem = "Me";
                    break;
                default:
                    break;
            }
        }
        else if (os.Platform == PlatformID.Win32NT)
        {
            switch (vs.Major)
            {
                case 3:
                    operatingSystem = "NT 3.51";
                    break;
                case 4:
                    operatingSystem = "NT 4.0";
                    break;
                case 5:
                    if (vs.Minor == 0)
                        operatingSystem = "2000";
                    else
                        operatingSystem = "XP";
                    break;
                case 6:
                    // IMPORTANT NOTE: If your executable assembly manifest doesn't explicitly state that your
                    // exe assembly is compatible with Windows 8.1 or Windows 10.0, System.Environment.OSVersion
                    // will return Windows 8 version, which is 6.2, instead of 6.3 and 10.0!
                    if (vs.Minor == 0)
                        operatingSystem = "Vista";
                    else if (vs.Minor == 1)
                        operatingSystem = "7";
                    else if (vs.Minor == 2)
                        operatingSystem = "8";
                    else
                        operatingSystem = "8.1";
                    break;
                case 10:
                    if (vs.Build >= 22000)
                        operatingSystem = "11";
                    else
                        operatingSystem = "10";
                    break;
                default:
                    break;
            }
        }
        // Make sure we actually got something in our OS check.
        // We don't want to just return " Service Pack 2" or " 32-bit".
        // That information is useless without the OS version.
        if (operatingSystem != "")
        {
            //Got something. Let's prepend "Windows" and get more info.
            operatingSystem = "Windows " + operatingSystem;
            //See if there's a service pack installed.
            if (os.ServicePack != "")
            {
                //Append it to the OS name.  i.e. "Windows XP Service Pack 3"
                operatingSystem += " " + os.ServicePack;
            }
            //Append the OS architecture.  i.e. "Windows XP Service Pack 3 32-bit"
            //operatingSystem += " " + getOSArchitecture().ToString() + "-bit";
        }
        //Return the information we've gathered.
        return operatingSystem;
    }

    /// <summary>
    /// Minimum supported client: Windows 2000 Professional
    /// Source: https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-osversioninfoexa
    /// IMPORTANT NOTE: If your executable assembly manifest doesn't explicitly state that your
    /// exe assembly is compatible with Windows 8.1 or Windows 10.0, System.Environment.OSVersion
    /// will return Windows 8 version, which is 6.2, instead of 6.3 and 10.0!
    /// </summary>
    /// <returns>tuple representing the OS major and minor version</returns>
    public static Version GetOSMajorAndMinor()
    {
        //OperatingSystem osVersion = Environment.OSVersion;
        OSVERSIONINFOEX osvi = new OSVERSIONINFOEX();
        osvi.dwOSVersionInfoSize = Marshal.SizeOf(typeof(OSVERSIONINFOEX));
        if (GetVersionEx(ref osvi))
        {
            int majorVersion = osvi.dwMajorVersion;
            int minorVersion = osvi.dwMinorVersion;
            int buildNumber = osvi.dwBuildNumber;
            int platformId = osvi.dwPlatformId;
            byte productType = osvi.wProductType;
            short suiteMask = osvi.wSuiteMask;
            string servicePack = osvi.szCSDVersion;
        }
        return new Version(osvi.dwMajorVersion, osvi.dwMinorVersion, osvi.dwBuildNumber);
    }

    [DllImport("kernel32.dll")]
    public static extern bool GetVersionEx(ref OSVERSIONINFOEX osVersionInfo);

    [StructLayout(LayoutKind.Sequential)]
    public struct OSVERSIONINFOEX
    {
        public int dwOSVersionInfoSize;
        public int dwMajorVersion;
        public int dwMinorVersion;
        public int dwBuildNumber;
        public int dwPlatformId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szCSDVersion;
        public short wServicePackMajor;
        public short wServicePackMinor;
        public short wSuiteMask;
        public byte wProductType;
        public byte wReserved;
    }
}

/// <summary>
/// This can also be achieved by passing <see cref="UTF8Encoding"/> to a <see cref="XmlTextWriter"/>.
/// </summary>
class StringWriterWithEncoding : StringWriter
	{
		private readonly Encoding encoding;

		public StringWriterWithEncoding(Encoding encoding) : base()
		{
			this.encoding = encoding;
		}

		public override Encoding Encoding
		{
			get
			{
				return this.encoding;
			}
		}
	}


/// <summary>
/// Extension methods for System.Threading.Tasks.Task and System.Threading.Tasks.ValueTask
/// https://devblogs.microsoft.com/dotnet/understanding-the-whys-whats-and-whens-of-valuetask/
/// </summary> 
public static partial class SafeFireAndForgetExtensions
{
    static Action<Exception>? _onException;
    static bool _shouldAlwaysRethrowException;

    /// <summary>
    /// NOTE: This is no ValueTask available in .NET 4.6.2
    /// Safely execute the ValueTask without waiting for it to complete before moving to the next line of code; commonly known as "Fire And Forget". Inspired by John Thiriet's blog post, "Removing Async Void": https://johnthiriet.com/removing-async-void/.
    /// </summary>
    /// <param name="task">ValueTask.</param>
    /// <param name="onException">If an exception is thrown in the ValueTask, <c>onException</c> will execute. If onException is null, the exception will be re-thrown</param>
    /// <param name="continueOnCapturedContext">If set to <c>true</c>, continue on captured context; this will ensure that the Synchronization Context returns to the calling thread. If set to <c>false</c>, continue on a different context; this will allow the Synchronization Context to continue on a different thread</param>
    public static void SafeFireAndForget(this ValueTask task, in Action<Exception>? onException = null, in bool continueOnCapturedContext = false) => HandleSafeFireAndForget(task, continueOnCapturedContext, onException);


    /// <summary>
    /// NOTE: This is no ValueTask available in .NET 4.6.2
    /// Safely execute the ValueTask without waiting for it to complete before moving to the next line of code; commonly known as "Fire And Forget". Inspired by John Thiriet's blog post, "Removing Async Void": https://johnthiriet.com/removing-async-void/.
    /// </summary>
    /// <param name="task">ValueTask.</param>
    /// <param name="onException">If an exception is thrown in the Task, <c>onException</c> will execute. If onException is null, the exception will be re-thrown</param>
    /// <param name="continueOnCapturedContext">If set to <c>true</c>, continue on captured context; this will ensure that the Synchronization Context returns to the calling thread. If set to <c>false</c>, continue on a different context; this will allow the Synchronization Context to continue on a different thread</param>
    /// <typeparam name="TException">Exception type. If an exception is thrown of a different type, it will not be handled</typeparam>
    public static void SafeFireAndForget<TException>(this ValueTask task, in Action<TException>? onException = null, in bool continueOnCapturedContext = false) where TException : Exception => HandleSafeFireAndForget(task, continueOnCapturedContext, onException);

#if NET8_0_OR_GREATER
	    /// <summary>
	    /// Safely execute the Task without waiting for it to complete before moving to the next line of code; commonly known as "Fire And Forget". Inspired by John Thiriet's blog post, "Removing Async Void": https://johnthiriet.com/removing-async-void/.
	    /// </summary>
	    /// <param name="task">Task.</param>
	    /// <param name="onException">If an exception is thrown in the Task, <c>onException</c> will execute. If onException is null, the exception will be re-thrown</param>
	    /// <param name="configureAwaitOptions">Options to control behavior when awaiting</param>
	    public static void SafeFireAndForget(this Task task, in ConfigureAwaitOptions configureAwaitOptions, in Action<Exception>? onException = null) => HandleSafeFireAndForget(task, configureAwaitOptions, onException);

	    /// <summary>
	    /// Safely execute the Task without waiting for it to complete before moving to the next line of code; commonly known as "Fire And Forget". Inspired by John Thiriet's blog post, "Removing Async Void": https://johnthiriet.com/removing-async-void/.
	    /// </summary>
	    /// <param name="task">Task.</param>
	    /// <param name="onException">If an exception is thrown in the Task, <c>onException</c> will execute. If onException is null, the exception will be re-thrown</param>
	    /// <param name="configureAwaitOptions">Options to control behavior when awaiting</param>
	    /// <typeparam name="TException">Exception type. If an exception is thrown of a different type, it will not be handled</typeparam>
	    public static void SafeFireAndForget<TException>(this Task task, in ConfigureAwaitOptions configureAwaitOptions, in Action<TException>? onException = null) where TException : Exception => HandleSafeFireAndForget(task, configureAwaitOptions, onException);
#endif

    /// <summary>
    /// Safely execute the Task without waiting for it to complete before moving to the next line of code; commonly known as "Fire And Forget". Inspired by John Thiriet's blog post, "Removing Async Void": https://johnthiriet.com/removing-async-void/.
    /// </summary>
    /// <param name="task">Task.</param>
    /// <param name="onException">If an exception is thrown in the Task, <c>onException</c> will execute. If onException is null, the exception will be re-thrown</param>
    /// <param name="continueOnCapturedContext">If set to <c>true</c>, continue on captured context; this will ensure that the Synchronization Context returns to the calling thread. If set to <c>false</c>, continue on a different context; this will allow the Synchronization Context to continue on a different thread</param>
    public static void SafeFireAndForget(this Task task, in Action<Exception>? onException = null, in bool continueOnCapturedContext = false) => HandleSafeFireAndForget(task, continueOnCapturedContext, onException);

    /// <summary>
    /// Safely execute the Task without waiting for it to complete before moving to the next line of code; commonly known as "Fire And Forget". Inspired by John Thiriet's blog post, "Removing Async Void": https://johnthiriet.com/removing-async-void/.
    /// </summary>
    /// <param name="task">Task.</param>
    /// <param name="onException">If an exception is thrown in the Task, <c>onException</c> will execute. If onException is null, the exception will be re-thrown</param>
    /// <param name="continueOnCapturedContext">If set to <c>true</c>, continue on captured context; this will ensure that the Synchronization Context returns to the calling thread. If set to <c>false</c>, continue on a different context; this will allow the Synchronization Context to continue on a different thread</param>
    /// <typeparam name="TException">Exception type. If an exception is thrown of a different type, it will not be handled</typeparam>
    public static void SafeFireAndForget<TException>(this Task task, in Action<TException>? onException = null, in bool continueOnCapturedContext = false) where TException : Exception => HandleSafeFireAndForget(task, continueOnCapturedContext, onException);

    /// <summary>
    /// Initialize SafeFireAndForget
    ///
    /// Warning: When <c>true</c>, there is no way to catch this exception and it will always result in a crash. Recommended only for debugging purposes.
    /// </summary>
    /// <param name="shouldAlwaysRethrowException">If set to <c>true</c>, after the exception has been caught and handled, the exception will always be rethrown.</param>
    public static void Initialize(in bool shouldAlwaysRethrowException = false) => _shouldAlwaysRethrowException = shouldAlwaysRethrowException;

    /// <summary>
    /// Remove the default action for SafeFireAndForget
    /// </summary>
    public static void RemoveDefaultExceptionHandling() => _onException = null;

    /// <summary>
    /// Set the default action for SafeFireAndForget to handle every exception
    /// </summary>
    /// <param name="onException">If an exception is thrown in the Task using SafeFireAndForget, <c>onException</c> will execute</param>
    public static void SetDefaultExceptionHandling(in Action<Exception> onException)
    {
        if (onException is null)
            throw new ArgumentNullException(nameof(onException));

        _onException = onException;
    }

    /// <summary>
    /// NOTE: This is no ValueTask available in .NET 4.7 (you'll need to install the System.Threading.Tasks.Extensions NuGet).
    /// </summary>
    static async void HandleSafeFireAndForget<TException>(ValueTask valueTask, bool continueOnCapturedContext, Action<TException>? onException) where TException : Exception
    {
        try
        {
            await valueTask.ConfigureAwait(continueOnCapturedContext);
        }
        catch (TException ex) when (_onException is not null || onException is not null)
        {
            HandleException(ex, onException);
    
            if (_shouldAlwaysRethrowException)
                throw;
        }
    }

    static async void HandleSafeFireAndForget<TException>(Task task, bool continueOnCapturedContext, Action<TException>? onException) where TException : Exception
    {
        try
        {
            await task.ConfigureAwait(continueOnCapturedContext);
        }
        catch (TException ex) when (_onException is not null || onException is not null)
        {
            HandleException(ex, onException);

            if (_shouldAlwaysRethrowException)
                throw;
        }
    }

#if NET8_0_OR_GREATER
	    static async void HandleSafeFireAndForget<TException>(Task task, ConfigureAwaitOptions configureAwaitOptions, Action<TException>? onException) where TException : Exception
	    {
		    try
		    {
			    await task.ConfigureAwait(configureAwaitOptions);
		    }
		    catch (TException ex) when (_onException is not null || onException is not null)
		    {
			    HandleException(ex, onException);

			    if (_shouldAlwaysRethrowException)
				    throw;
		    }
	    }
#endif

    static void HandleException<TException>(in TException exception, in Action<TException>? onException) where TException : Exception
    {
        _onException?.Invoke(exception);
        onException?.Invoke(exception);
    }
}

/// <summary>
/// You can also use the <see cref="Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)"/>.
/// </summary>
public static class KnownFolders
{
    public static readonly Guid AddNewPrograms = new Guid("de61d971-5ebc-4f02-a3a9-6c82895e5c04");
    public static readonly Guid AdminTools = new Guid("724EF170-A42D-4FEF-9F26-B60E846FBA4F");
    public static readonly Guid AppUpdates = new Guid("a305ce99-f527-492b-8b1a-7e76fa98d6e4");
    public static readonly Guid CDBurning = new Guid("9E52AB10-F80D-49DF-ACB8-4330F5687855");
    public static readonly Guid ChangeRemovePrograms = new Guid("df7266ac-9274-4867-8d55-3bd661de872d");
    public static readonly Guid CommonAdminTools = new Guid("D0384E7D-BAC3-4797-8F14-CBA229B392B5");
    public static readonly Guid CommonOEMLinks = new Guid("C1BAE2D0-10DF-4334-BEDD-7AA20B227A9D");
    public static readonly Guid CommonPrograms = new Guid("0139D44E-6AFE-49F2-8690-3DAFCAE6FFB8");
    public static readonly Guid CommonStartMenu = new Guid("A4115719-D62E-491D-AA7C-E74B8BE3B067");
    public static readonly Guid CommonStartup = new Guid("82A5EA35-D9CD-47C5-9629-E15D2F714E6E");
    public static readonly Guid CommonTemplates = new Guid("B94237E7-57AC-4347-9151-B08C6C32D1F7");
    public static readonly Guid ComputerFolder = new Guid("0AC0837C-BBF8-452A-850D-79D08E667CA7");
    public static readonly Guid ConflictFolder = new Guid("4bfefb45-347d-4006-a5be-ac0cb0567192");
    public static readonly Guid ConnectionsFolder = new Guid("6F0CD92B-2E97-45D1-88FF-B0D186B8DEDD");
    public static readonly Guid Contacts = new Guid("56784854-C6CB-462b-8169-88E350ACB882");
    public static readonly Guid ControlPanelFolder = new Guid("82A74AEB-AEB4-465C-A014-D097EE346D63");
    public static readonly Guid Cookies = new Guid("2B0F765D-C0E9-4171-908E-08A611B84FF6");
    public static readonly Guid Desktop = new Guid("B4BFCC3A-DB2C-424C-B029-7FE99A87C641");
    public static readonly Guid Documents = new Guid("FDD39AD0-238F-46AF-ADB4-6C85480369C7");
    public static readonly Guid Downloads = new Guid("374DE290-123F-4565-9164-39C4925E467B");
    public static readonly Guid Favorites = new Guid("1777F761-68AD-4D8A-87BD-30B759FA33DD");
    public static readonly Guid Fonts = new Guid("FD228CB7-AE11-4AE3-864C-16F3910AB8FE");
    public static readonly Guid Games = new Guid("CAC52C1A-B53D-4edc-92D7-6B2E8AC19434");
    public static readonly Guid GameTasks = new Guid("054FAE61-4DD8-4787-80B6-090220C4B700");
    public static readonly Guid History = new Guid("D9DC8A3B-B784-432E-A781-5A1130A75963");
    public static readonly Guid InternetCache = new Guid("352481E8-33BE-4251-BA85-6007CAEDCF9D");
    public static readonly Guid InternetFolder = new Guid("4D9F7874-4E0C-4904-967B-40B0D20C3E4B");
    public static readonly Guid Links = new Guid("bfb9d5e0-c6a9-404c-b2b2-ae6db6af4968");
    public static readonly Guid LocalAppData = new Guid("F1B32785-6FBA-4FCF-9D55-7B8E7F157091");
    public static readonly Guid LocalAppDataLow = new Guid("A520A1A4-1780-4FF6-BD18-167343C5AF16");
    public static readonly Guid LocalizedResourcesDir = new Guid("2A00375E-224C-49DE-B8D1-440DF7EF3DDC");
    public static readonly Guid Music = new Guid("4BD8D571-6D19-48D3-BE97-422220080E43");
    public static readonly Guid NetHood = new Guid("C5ABBF53-E17F-4121-8900-86626FC2C973");
    public static readonly Guid NetworkFolder = new Guid("D20BEEC4-5CA8-4905-AE3B-BF251EA09B53");
    public static readonly Guid OriginalImages = new Guid("2C36C0AA-5812-4b87-BFD0-4CD0DFB19B39");
    public static readonly Guid PhotoAlbums = new Guid("69D2CF90-FC33-4FB7-9A0C-EBB0F0FCB43C");
    public static readonly Guid Pictures = new Guid("33E28130-4E1E-4676-835A-98395C3BC3BB");
    public static readonly Guid Playlists = new Guid("DE92C1C7-837F-4F69-A3BB-86E631204A23");
    public static readonly Guid PrintersFolder = new Guid("76FC4E2D-D6AD-4519-A663-37BD56068185");
    public static readonly Guid PrintHood = new Guid("9274BD8D-CFD1-41C3-B35E-B13F55A758F4");
    public static readonly Guid Profile = new Guid("5E6C858F-0E22-4760-9AFE-EA3317B67173");
    public static readonly Guid ProgramData = new Guid("62AB5D82-FDC1-4DC3-A9DD-070D1D495D97");
    public static readonly Guid ProgramFiles = new Guid("905e63b6-c1bf-494e-b29c-65b732d3d21a");
    public static readonly Guid ProgramFilesX64 = new Guid("6D809377-6AF0-444b-8957-A3773F02200E");
    public static readonly Guid ProgramFilesX86 = new Guid("7C5A40EF-A0FB-4BFC-874A-C0F2E0B9FA8E");
    public static readonly Guid ProgramFilesCommon = new Guid("F7F1ED05-9F6D-47A2-AAAE-29D317C6F066");
    public static readonly Guid ProgramFilesCommonX64 = new Guid("6365D5A7-0F0D-45E5-87F6-0DA56B6A4F7D");
    public static readonly Guid ProgramFilesCommonX86 = new Guid("DE974D24-D9C6-4D3E-BF91-F4455120B917");
    public static readonly Guid Programs = new Guid("A77F5D77-2E2B-44C3-A6A2-ABA601054A51");
    public static readonly Guid Public = new Guid("DFDF76A2-C82A-4D63-906A-5644AC457385");
    public static readonly Guid PublicDesktop = new Guid("C4AA340D-F20F-4863-AFEF-F87EF2E6BA25");
    public static readonly Guid PublicDocuments = new Guid("ED4824AF-DCE4-45A8-81E2-FC7965083634");
    public static readonly Guid PublicDownloads = new Guid("3D644C9B-1FB8-4f30-9B45-F670235F79C0");
    public static readonly Guid PublicGameTasks = new Guid("DEBF2536-E1A8-4c59-B6A2-414586476AEA");
    public static readonly Guid PublicMusic = new Guid("3214FAB5-9757-4298-BB61-92A9DEAA44FF");
    public static readonly Guid PublicPictures = new Guid("B6EBFB86-6907-413C-9AF7-4FC2ABF07CC5");
    public static readonly Guid PublicVideos = new Guid("2400183A-6185-49FB-A2D8-4A392A602BA3");
    public static readonly Guid QuickLaunch = new Guid("52a4f021-7b75-48a9-9f6b-4b87a210bc8f");
    public static readonly Guid Recent = new Guid("AE50C081-EBD2-438A-8655-8A092E34987A");
    public static readonly Guid RecycleBinFolder = new Guid("B7534046-3ECB-4C18-BE4E-64CD4CB7D6AC");
    public static readonly Guid ResourceDir = new Guid("8AD10C31-2ADB-4296-A8F7-E4701232C972");
    public static readonly Guid RoamingAppData = new Guid("3EB685DB-65F9-4CF6-A03A-E3EF65729F3D");
    public static readonly Guid SampleMusic = new Guid("B250C668-F57D-4EE1-A63C-290EE7D1AA1F");
    public static readonly Guid SamplePictures = new Guid("C4900540-2379-4C75-844B-64E6FAF8716B");
    public static readonly Guid SamplePlaylists = new Guid("15CA69B3-30EE-49C1-ACE1-6B5EC372AFB5");
    public static readonly Guid SampleVideos = new Guid("859EAD94-2E85-48AD-A71A-0969CB56A6CD");
    public static readonly Guid SavedGames = new Guid("4C5C32FF-BB9D-43b0-B5B4-2D72E54EAAA4");
    public static readonly Guid SavedSearches = new Guid("7d1d3a04-debb-4115-95cf-2f29da2920da");
    public static readonly Guid SEARCH_CSC = new Guid("ee32e446-31ca-4aba-814f-a5ebd2fd6d5e");
    public static readonly Guid SEARCH_MAPI = new Guid("98ec0e18-2098-4d44-8644-66979315a281");
    public static readonly Guid SearchHome = new Guid("190337d1-b8ca-4121-a639-6d472d16972a");
    public static readonly Guid SendTo = new Guid("8983036C-27C0-404B-8F08-102D10DCFD74");
    public static readonly Guid SidebarDefaultParts = new Guid("7B396E54-9EC5-4300-BE0A-2482EBAE1A26");
    public static readonly Guid SidebarParts = new Guid("A75D362E-50FC-4fb7-AC2C-A8BEAA314493");
    public static readonly Guid StartMenu = new Guid("625B53C3-AB48-4EC1-BA1F-A1EF4146FC19");
    public static readonly Guid Startup = new Guid("B97D20BB-F46A-4C97-BA10-5E3608430854");
    public static readonly Guid SyncManagerFolder = new Guid("43668BF8-C14E-49B2-97C9-747784D784B7");
    public static readonly Guid SyncResultsFolder = new Guid("289a9a43-be44-4057-a41b-587a76d7e7f9");
    public static readonly Guid SyncSetupFolder = new Guid("0F214138-B1D3-4a90-BBA9-27CBC0C5389A");
    public static readonly Guid System = new Guid("1AC14E77-02E7-4E5D-B744-2EB1AE5198B7");
    public static readonly Guid SystemX86 = new Guid("D65231B0-B2F1-4857-A4CE-A8E7C6EA7D27");
    public static readonly Guid Templates = new Guid("A63293E8-664E-48DB-A079-DF759E0509F7");
    public static readonly Guid TreeProperties = new Guid("5b3749ad-b49f-49c1-83eb-15370fbd4882");
    public static readonly Guid UserProfiles = new Guid("0762D272-C50A-4BB0-A382-697DCD729B80");
    public static readonly Guid UsersFiles = new Guid("f3ce0f7c-4901-4acc-8648-d5d44b04ef8f");
    public static readonly Guid Videos = new Guid("18989B1D-99B5-455B-841C-AB7C74E4DDFC");
    public static readonly Guid Windows = new Guid("F38BF404-1D43-42F2-9305-67DE0B28FC23");

    public static string GetKnownPath(Guid knownFolder)
    {
        try
        {
            return SHGetKnownFolderPath(knownFolder, 0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetKnownPath]: {ex.Message}");
            return string.Empty;
        }
    }

    [DllImport("shell32", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = false)]
    static extern string SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, nint hToken = 0);
}
