using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using Microsoft.Win32;

/// <summary>
/// Represents a program that is installed on a PC
/// </summary>
public class InstalledProgram : IComparable<InstalledProgram>, IEquatable<InstalledProgram>
{


    #region "Properties"

    private string _DisplayName = string.Empty;
    /// <summary>
    /// The name that would be displayed in Add/Remove Programs
    /// </summary>
    public string DisplayName
    {
        get { return _DisplayName; }
        set { _DisplayName = value; }
    }

    private string _Version = string.Empty;
    /// <summary>
    /// The version of the program
    /// </summary>
    public string Version
    {
        get { return _Version; }
        set { _Version = value; }
    }

    private string _ParentDisplayName = string.Empty;
    /// <summary>
    /// If this program is an update then this will contain the display name of the product that 
    /// this is an update to
    /// </summary>
    public string ParentDisplayName
    {
        get { return _ParentDisplayName; }
        set { _ParentDisplayName = value; }
    }

    private bool _IsUpdate;
    /// <summary>
    /// Is this program classed as an update 
    /// </summary>
    public bool IsUpdate
    {
        get { return _IsUpdate; }
        set { _IsUpdate = value; }
    }

    private string _UninstallString;
    public string UninstallString
    {
        get { return _UninstallString; }
       set { _UninstallString = value; }
    }

    private string _Guid;
    public string Guid
    {
        get { return _Guid; }
        set { _Guid = value; }
    }
    #endregion

    #region "Constructors"

    public InstalledProgram(string ProgramDisplayName, string UninstallString)
    {
        this.DisplayName = ProgramDisplayName;
        this._UninstallString = UninstallString;
    }

    public InstalledProgram(string ProgramDisplayName)
    {
        this.DisplayName = ProgramDisplayName;
    }
    public InstalledProgram(string ProgramDisplayName, string ProgramParentDisplayName, bool IsProgramUpdate, string ProgramVersion)
    {
        this.DisplayName = ProgramDisplayName;
        this.ParentDisplayName = ProgramParentDisplayName;
        this.IsUpdate = IsProgramUpdate;
        this.Version = ProgramVersion;
    }

    #endregion

    #region "Public Methods"

    public override string ToString()
    {
        return DisplayName;
    }

    /// <summary>
    /// Retrieves a list of all installed programs on the local computer
    /// </summary>
    public static List<InstalledProgram> GetInstalledPrograms(bool IncludeUpdates)
    {
        return InternalGetInstalledPrograms(IncludeUpdates, Registry.LocalMachine, Registry.Users);
    }

    /// <summary>
    /// Retrieves a list of all installed programs on the specified computer
    /// </summary>
    /// <param name="ComputerName">The name of the computer to get the list of installed programs from</param>
    /// <param name="IncludeUpdates">Determines whether or not updates for installed programs are included in the list</param>
    public static List<InstalledProgram> GetInstalledPrograms(string ComputerName, bool IncludeUpdates)
    {
        try
        {
            return InternalGetInstalledPrograms(IncludeUpdates, RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, ComputerName), RegistryKey.OpenRemoteBaseKey(RegistryHive.Users, ComputerName));
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            return new List<InstalledProgram>();
        }
    }

    //Sorting function, required by IComparable interface
    public int CompareTo(InstalledProgram other)
    {
        return string.Compare(this.DisplayName, other.DisplayName);
    }

    //Equality function, required by IEquatable interface
    public bool Equals1(InstalledProgram other)
    {
        if (this.DisplayName == other.DisplayName && this.Version == other.Version)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
    bool System.IEquatable<InstalledProgram>.Equals(InstalledProgram other)
    {
        return Equals1(other);
    }

    #endregion

    #region "Private Methods"

    private static List<InstalledProgram> InternalGetInstalledPrograms(bool IncludeUpdates, RegistryKey HklmPath, RegistryKey HkuPath)
    {
        List<InstalledProgram> ProgramList = new List<InstalledProgram>();

        RegistryKey ClassesKey = HklmPath.OpenSubKey("Software\\Classes\\Installer\\Products");

        //---Wow64 Uninstall key
        RegistryKey Wow64UninstallKey = HklmPath.OpenSubKey("Software\\Wow6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall");
        ProgramList = GetUninstallKeyPrograms(Wow64UninstallKey, ClassesKey, ProgramList, IncludeUpdates);

        //---Standard Uninstall key
        RegistryKey StdUninstallKey = HklmPath.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall");
        ProgramList = GetUninstallKeyPrograms(StdUninstallKey, ClassesKey, ProgramList, IncludeUpdates);

        foreach (string UserSid in HkuPath.GetSubKeyNames())
        {
            //---HKU Uninstall key
            RegistryKey CuUnInstallKey = HkuPath.OpenSubKey(UserSid + "\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall");
            ProgramList = GetUninstallKeyPrograms(CuUnInstallKey, ClassesKey, ProgramList, IncludeUpdates);
            //---HKU Installer key
            RegistryKey CuInstallerKey = HkuPath.OpenSubKey(UserSid + "\\Software\\Microsoft\\Installer\\Products");
            ProgramList = GetUserInstallerKeyPrograms(CuInstallerKey, HklmPath, ProgramList);
        }

        //Close the registry keys
        try
        {
            HklmPath.Close();
            HkuPath.Close();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error closing registry key - " + ex.Message);
        }

        //Sort the list alphabetically and return it to the caller
        //ProgramList.Sort();
        return ProgramList;
    }

    private static bool IsProgramInList(string ProgramName, List<InstalledProgram> ListToCheck)
    {
        return ListToCheck.Contains(new InstalledProgram(ProgramName));
    }

    private static List<InstalledProgram> GetUserInstallerKeyPrograms(RegistryKey CuInstallerKey, RegistryKey HklmRootKey, List<InstalledProgram> ExistingProgramList)
    {
        if ((CuInstallerKey != null))
        {
            foreach (string CuProductGuid in CuInstallerKey.GetSubKeyNames())
            {
                bool ProductFound = false;
                var data = HklmRootKey.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Installer\\UserData");
                if (data == null)
                {
                    continue;
                }
                foreach (string UserDataKeyName in data.GetSubKeyNames())
                {
                    //Ignore the LocalSystem account
                    if (!(UserDataKeyName == "S-1-5-18"))
                    {
                        RegistryKey ProductsKey = HklmRootKey.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Installer\\UserData\\" + UserDataKeyName + "\\Products");
                        if ((ProductsKey != null))
                        {
                            string[] LmProductGuids = ProductsKey.GetSubKeyNames();
                            foreach (string LmProductGuid in LmProductGuids)
                            {
                                if (LmProductGuid == CuProductGuid)
                                {
                                    RegistryKey UserDataProgramKey = HklmRootKey.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Installer\\UserData\\" + UserDataKeyName + "\\Products\\" + LmProductGuid + "\\InstallProperties");
                                    if (!(Convert.ToInt32(UserDataProgramKey.GetValue("SystemComponent", 0)) == 1))
                                    {
                                        string Name = Convert.ToString(CuInstallerKey.OpenSubKey(CuProductGuid).GetValue("ProductName", string.Empty));
                                        string Uninstall = Convert.ToString(CuInstallerKey.OpenSubKey(CuProductGuid).GetValue("UninstallString", string.Empty));                                        string ProgVersion = string.Empty;
                                        
                                        try
                                        {
                                            ProgVersion = Convert.ToString(UserDataProgramKey.GetValue("DisplayVersion", string.Empty));
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.WriteLine(ex.Message);
                                        }
                                        if (!(Name == string.Empty) && !IsProgramInList(Name, ExistingProgramList))
                                        {
                                            var prog = new InstalledProgram(Name, Uninstall);
                                          var guid= System.Guid.Parse(LmProductGuid);
                                            var temp= guid.ToString("B");
                                            var real = System.Guid.Parse(GetInstallerKeyNameFromGuid(temp)).ToString("B");
                                            prog.Guid = real;
                                            ExistingProgramList.Add(prog);
                                            ProductFound = true;
                                        }
                                    }
                                    break; // TODO: might not be correct. Was : Exit For
                                }
                            }
                            if (ProductFound)
                            {
                                break; // TODO: might not be correct. Was : Exit For
                            }
                            try
                            {
                                ProductsKey.Close();
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(ex.Message);
                            }
                        }
                    }
                }
            }
        }
        return ExistingProgramList;
    }

    private static List<InstalledProgram> GetUninstallKeyPrograms(RegistryKey UninstallKey, RegistryKey ClassesKey, List<InstalledProgram> ExistingProgramList, bool IncludeUpdates)
    {
        //Make sure the key exists
        if ((UninstallKey != null))
        {
            //Loop through all subkeys (each one represents an installed program)
            foreach (string SubKeyName in UninstallKey.GetSubKeyNames())
            {
                try
                {
                    RegistryKey CurrentSubKey = UninstallKey.OpenSubKey(SubKeyName);
                    //Skip this program if the SystemComponent flag is set
                    int IsSystemComponent = 0;
                    bool ErrorCheckingSystemComponent = false;
                    try
                    {
                        IsSystemComponent = Convert.ToInt32(CurrentSubKey.GetValue("SystemComponent", 0));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(SubKeyName + " - " + ex.Message);
                    }
                    if (!(IsSystemComponent == 1))
                    {
                        //If the WindowsInstaller flag is set then add the key name to our list of Windows Installer GUIDs
                        if (!(Convert.ToInt32(CurrentSubKey.GetValue("WindowsInstaller", 0)) == 1))
                        {
                            System.Text.RegularExpressions.Regex WindowsUpdateRegEx = new System.Text.RegularExpressions.Regex("KB[0-9]{6}$");
                            string ProgramReleaseType = Convert.ToString(CurrentSubKey.GetValue("ReleaseType", string.Empty));
                            string ProgVersion = string.Empty;
                            try
                            {
                                ProgVersion = Convert.ToString(CurrentSubKey.GetValue("DisplayVersion", string.Empty));
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(SubKeyName + " - " + ex.Message);
                            }
                            //Check to see if this program is classed as an update
                            if (WindowsUpdateRegEx.Match(SubKeyName).Success == true || !(Convert.ToString(CurrentSubKey.GetValue("ParentKeyName", string.Empty)) == string.Empty) || ProgramReleaseType == "Security Update" || ProgramReleaseType == "Update Rollup" || ProgramReleaseType == "Hotfix")
                            {
                                if (IncludeUpdates)
                                {
                                    //Add the program to our list if we are including updates in this search
                                    string Name = Convert.ToString(CurrentSubKey.GetValue("DisplayName", string.Empty));
                                    string uninstall = Convert.ToString(CurrentSubKey.GetValue("UninstallString", string.Empty));
                                        
                                    if (!(Name == string.Empty) && !IsProgramInList(Name, ExistingProgramList))
                                    {
                                      var ip= new InstalledProgram(Name, Convert.ToString(CurrentSubKey.GetValue("ParentDisplayName", string.Empty)), true, ProgVersion);
                                        ip.UninstallString = uninstall;
                                        ExistingProgramList.Add(ip);

                                    }
                                }
                                //If not classed as an update
                            }
                            else
                            {
                                bool UninstallStringExists = false;
                                foreach (string valuename in CurrentSubKey.GetValueNames())
                                {
                                    if (string.Compare("UninstallString", valuename, true) == 0)
                                    {
                                        UninstallStringExists = true;
                                        break; // TODO: might not be correct. Was : Exit For
                                    }
                                }
                                if (UninstallStringExists)
                                {
                                    string Name = Convert.ToString(CurrentSubKey.GetValue("DisplayName", string.Empty));
                                    string uninstall = Convert.ToString(CurrentSubKey.GetValue("UninstallString", string.Empty));
                                    if (!(Name == string.Empty) && !IsProgramInList(Name, ExistingProgramList))
                                    {
                                        var ip=new InstalledProgram(Name, Convert.ToString(CurrentSubKey.GetValue("ParentDisplayName", string.Empty)), false, ProgVersion);
                                        ip.UninstallString = uninstall;
                                        ExistingProgramList.Add(ip);
                                    }
                                }
                            }
                            //If WindowsInstaller
                        }
                        else
                        {
                            string ProgVersion = string.Empty;
                            string Name = string.Empty;
                            string Uninstall = string.Empty;
                            string Guid = string.Empty;
                            bool FoundName = false;
                            try
                            {
                                string MsiKeyName = GetInstallerKeyNameFromGuid(SubKeyName);
                                RegistryKey CrGuidKey = ClassesKey.OpenSubKey(MsiKeyName);
                                if ((CrGuidKey != null))
                                {
                                    Name = Convert.ToString(CrGuidKey.GetValue("ProductName", string.Empty));
                                    Uninstall = Convert.ToString(CurrentSubKey.GetValue("UninstallString", string.Empty));
                                    Guid = SubKeyName;
                                }

                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(SubKeyName + " - " + ex.Message);
                            }
                            try
                            {
                                ProgVersion = Convert.ToString(CurrentSubKey.GetValue("DisplayVersion", string.Empty));
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(ex.Message);
                            }
                            if (!(Name == string.Empty) && !IsProgramInList(Name, ExistingProgramList))
                            {
                                var ip = new InstalledProgram(Name, Convert.ToString(CurrentSubKey.GetValue("ParentDisplayName", string.Empty)), false, ProgVersion);
                                ip.UninstallString = Uninstall;
                                ip.Guid = Guid;
                                ExistingProgramList.Add(ip);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(SubKeyName + " - " + ex.Message);
                }
            }
            //Close the registry key
            try
            {
                UninstallKey.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }
        return ExistingProgramList;
    }

    private static string GetInstallerKeyNameFromGuid(string GuidName)
    {
        string[] MsiNameParts = GuidName.Replace("{", "").Replace("}", "").Split('-');
        System.Text.StringBuilder MsiName = new System.Text.StringBuilder();
        //Just reverse the first 3 parts
        for (int i = 0; i <= 2; i++)
        {
            MsiName.Append(ReverseString(MsiNameParts[i]));
        }
        //For the last 2 parts, reverse each character pair
        for (int j = 3; j <= 4; j++)
        {
            for (int i = 0; i <= MsiNameParts[j].Length - 1; i++)
            {
                MsiName.Append(MsiNameParts[j][i + 1]);
                MsiName.Append(MsiNameParts[j][i]);
                i += 1;
            }
        }
        return MsiName.ToString();
    }

    private static string ReverseString(string input)
    {
        char[] Chars = input.ToCharArray();
        Array.Reverse(Chars);
        return new string(Chars);
    }

    #endregion


}
