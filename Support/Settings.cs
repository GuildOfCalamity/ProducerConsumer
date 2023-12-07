using System.Runtime.Serialization;

namespace ProducerConsumer;

/// <summary>
/// This is our serializable class for storing app settings.
/// </summary>
/// <remarks>
/// My original version did not allow direct access to the 
/// <see cref="_Settings"/> member. I have modified this to now
/// instance the <see cref="_Settings"/>. This configuration allows
/// the user to directly access the settings without having to reload
/// from disk. Calling <see cref="GetSettings(string)"/> will return 
/// the current <see cref="_Settings"/> member.
/// </remarks>
[DataContract]
public class Settings
{
    private string _FontName = "Consolas";
    private int _FontSize = 32;
    private int _TestNumber = 1;
    private Settings? _Settings = null;

    public Settings()
    {
    }

    public Settings(string fontName, int fontSize, int testNumber)
    {
        _FontName = fontName;
        _FontSize = fontSize;
        _TestNumber = testNumber;
    }

    [DataMember]
    public string FontName
    {
        get { return _FontName; }
        set { _FontName = value; }
    }

    [DataMember]
    public int FontSize
    {
        get { return _FontSize; }
        set { _FontSize = value; }
    }

    [DataMember]
    public int TestNumber
    {
        get { return _TestNumber; }
        set { _TestNumber = value; }
    }

    /// <summary>
    /// Load system settings from disk.
    /// </summary>
    /// <param name="fileName">name of config file</param>
    /// <param name="path">optional directory path</param>
    /// <returns><see cref="Settings"/> object</returns>
    public Settings GetSettings(string fileName, string path = "")
    {
        try
        {
            if (_Settings == null && !string.IsNullOrEmpty(fileName))
            {
                if (string.IsNullOrEmpty(path))
                    path = Directory.GetCurrentDirectory();

                if (File.Exists(Path.Combine(path, fileName)))
                {
                    string imported = Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(path, fileName)));
                    Debug.WriteLine($"⇒ Config loaded: {imported.Truncate(40)}");
                    _Settings = Utils.FromJsonTo<Settings>(imported);
                }
                else
                {
                    Debug.WriteLine($"⇒ No config was found, creating default config.");
                    _Settings = new Settings("Consolas", 24, 99);
                    SaveSettings(fileName);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⇒ LoadSettings(ERROR): {ex.Message}");
        }

        // If there was any issue during the process just default the settings.
        if (_Settings == null)
            _Settings = new Settings("Consolas", 24, 99);

        return _Settings;
    }

    /// <summary>
    /// Save system settings to disk.
    /// </summary>
    /// <param name="sysSettings"><see cref="SerialConfig"/> object</param>
    /// <param name="fileName">name of config file</param>
    /// <param name="path">optional directory path</param>
    /// <returns>true is successful, false otherwise</returns>
    public bool SaveSettings(string fileName, string path = "")
    {
        try
        {
            if (_Settings != null && !string.IsNullOrEmpty(fileName))
            {
                if (string.IsNullOrEmpty(path))
                    path = Directory.GetCurrentDirectory();

                File.WriteAllBytes(System.IO.Path.Combine(path, fileName), Encoding.UTF8.GetBytes(Utils.ToJson(_Settings)));
                Debug.WriteLine($"⇒ Settings saved.");
                return true;
            }
            else
            {
                Debug.WriteLine($"⇒ Problem with saving the settings.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⇒ SaveSettings(ERROR): {ex.Message}");
        }

        return false;
    }

    #region [Extras]
    /// <summary>
    /// We can use our old friend Reflection to iterate through internal class members/properties.
    /// </summary>
    public IEnumerable<object?> ListSettings()
    {
        FieldInfo[] fields = typeof(Settings).GetFields(BindingFlags.Instance |
                                                        BindingFlags.Static |
                                                        BindingFlags.Public |
                                                        BindingFlags.NonPublic |
                                                        BindingFlags.FlattenHierarchy);
        foreach (FieldInfo field in fields)
        {
            if (field.IsStatic)
                yield return field.GetValue(field);
            else
                yield return GetInstanceField(typeof(Settings), this, field.Name);
        }
    }

    /// <summary>
    /// Uses reflection to get the field value from an object & type.
    /// </summary>
    /// <param name="type">The instance type.</param>
    /// <param name="instance">The instance object.</param>
    /// <param name="fieldName">The field's name which is to be fetched.</param>
    /// <returns>The field value from the object.</returns>
    internal static object? GetInstanceField(Type type, object instance, string fieldName)
    {
        // IN-LINE USAGE: var str = GetInstanceField(typeof(Settings), this, "FontName") as string;
        BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        FieldInfo? field = type.GetField(fieldName, bindFlags);
        return field == null ? null : field.GetValue(instance);
    }
    internal static object? GetInstanceField<T>(T instance, string fieldName)
    {
        // IN-LINE USAGE: var str = (string)GetInstanceField(instance, "FontName");
        BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        FieldInfo? field = typeof(T).GetField(fieldName, bindFlags);
        return field == null ? null : field.GetValue(instance);
    }
    #endregion [Support Methods]
}
