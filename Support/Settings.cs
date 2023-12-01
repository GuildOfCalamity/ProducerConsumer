using System.Runtime.Serialization;

namespace ProducerConsumer;

/// <summary>
/// This is our serializable class for storing app settings.
/// </summary>
/// 
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

    public Settings? AppSettings
    {
        get { return _Settings; }
        set { _Settings = value; }
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
    /// <param name="filePath">name of config file</param>
    /// <returns><see cref="Settings"/> object</returns>
    public Settings? GetSettings(string fileName)
    {
        try
        {
            if (_Settings == null && !string.IsNullOrEmpty(fileName))
            {
                if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), fileName)))
                {
                    string imported = Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(Directory.GetCurrentDirectory(), fileName)));
                    Debug.WriteLine($"⇒ Config loaded: {imported}");
                    _Settings = Utils.FromJsonTo<Settings>(imported);
                }
                else
                {
                    Debug.WriteLine($"⇒ No config was found, creating default config...");
                    _Settings = new Settings("Consolas", 24, 99);
                    SaveSettings(fileName);
                }
            }

            return _Settings;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⇒ LoadSettings(ERROR): {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Save system settings to disk.
    /// </summary>
    /// <param name="sysSettings"><see cref="SerialConfig"/> object</param>
    /// <param name="filePath">name of config file</param>
    /// <returns>true is successful, false otherwise</returns>
    public bool SaveSettings(string filePath)
    {
        try
        {
            if (_Settings != null && !string.IsNullOrEmpty(filePath))
            {
                File.WriteAllBytes(System.IO.Path.Combine(Directory.GetCurrentDirectory(), filePath), Encoding.UTF8.GetBytes(Utils.ToJson(_Settings)));
                Debug.WriteLine($"⇒ Settings saved.");
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⇒ SaveSettings(ERROR): {ex.Message}");
        }

        return false;
    }

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

    #region [Support Methods]
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
