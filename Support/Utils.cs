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

namespace ProducerConsumer;

public static class Utils
{
    public static string GetReadableTime(this TimeSpan timeSpan)
    {
        // Simplified version:
        //return string.Format("{0} day(s) {1} hour(s) {2} minute(s) {3} second(s) and {4} millisecond(s)", timeSpan.Days, timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds, timeSpan.Milliseconds);

        var parts = new StringBuilder();
        if (timeSpan.Days > 0)
            parts.AppendFormat("{0} {1} ", timeSpan.Days, timeSpan.Days == 1 ? "day" : "days");
        if (timeSpan.Hours > 0)
            parts.AppendFormat("{0} {1} ", timeSpan.Hours, timeSpan.Hours == 1 ? "hour" : "hours");
        if (timeSpan.Minutes > 0)
            parts.AppendFormat("{0} {1} ", timeSpan.Minutes, timeSpan.Minutes == 1 ? "minute" : "minutes");
        if (timeSpan.Seconds > 0)
            parts.AppendFormat("{0} {1} ", timeSpan.Seconds, timeSpan.Seconds == 1 ? "second" : "seconds");
        if (timeSpan.Milliseconds > 0)
            parts.AppendFormat("{0} {1}", timeSpan.Milliseconds, timeSpan.Milliseconds == 1 ? "millisecond" : "milliseconds");

        return parts.ToString().TrimEnd();
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
            blockingCollection.TryTake(out T item);
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

        while (blockCollection.TryTake(out T item))
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
        string retVal = names[Rnd.Next(names.Length)];
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
		static XElement RemoveAllNamespaces(XElement xmlDocument)
		{
			if (!xmlDocument.HasElements)
			{
				XElement xElement = new XElement(xmlDocument.Name.LocalName);
				xElement.Value = xmlDocument.Value;

				foreach (XAttribute attribute in xmlDocument.Attributes())
					xElement.Add(attribute);

				return xElement;
			}
			return new XElement(xmlDocument.Name.LocalName, xmlDocument.Elements().Select(el => RemoveAllNamespaces(el)));
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
    public static (int,int) GetOSMajorAndMinor()
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
        return (osvi.dwMajorVersion, osvi.dwMinorVersion);
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
