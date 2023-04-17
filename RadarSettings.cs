using System;
using ProtoBuf;
using Sandbox.ModAPI;

namespace RadarBlock
{

    [ProtoContract]
    public class Settings
    {

        [ProtoMember(1)]
        public string settingsVersion { get; set; }

        [ProtoMember(2)]
        public bool UseMaxOutput { get; set; }

        [ProtoMember(3)]
        public float ActiveMAXScanGridsMWthreshold { get; set; }

        [ProtoMember(4)]
        public float PassiveMAXScanGridsMWthreshold { get; set; }

        [ProtoMember(5)]
        public float ActiveCURRENTScanGridsMWthreshold { get; set; }

        [ProtoMember(6)]
        public float PassiveCURRENTScanGridsMWthreshold { get; set; }

        [ProtoMember(7)]
        public int DelayInSecondsActiveScan { get; set; }

        [ProtoMember(8)]
        public bool RequireStationaryWhileActiveScanning { get; set; }

        [ProtoMember(9)]
        public bool AllEnabledRadarsVisible { get; set; }

        [ProtoMember(10)]
        public int CharacterDetectionRange { get; set; }

        [ProtoMember(11)]
        public float ModeSwitchAtBroadcastRange { get; set; }

        [ProtoMember(12)]
        public int GpsOffSetDistanceMin { get; set; }

        [ProtoMember(13)]
        public int GpsOffSetDistanceMax { get; set; }

        [ProtoMember(14)]
        public bool EnableSmallRadar { get; set; }

        [ProtoMember(15)]
        public bool AllowSonar { get; set; }

        [ProtoMember(16)]
        public bool SaveGpsOnActiveScan { get; set; }

        [ProtoMember(17)]
        public bool OutputToLCD { get; set; }

        [ProtoMember(18)]
        public bool OmitOfflineEntities { get; set; }

        [ProtoMember(19)]
        public bool OmitOfflineEntitiesFromSonarMode { get; set; }


        public Settings()
        {

            settingsVersion = "1.01 *Do Not Change This*";
            UseMaxOutput = true;
            ActiveMAXScanGridsMWthreshold = 16;
            PassiveMAXScanGridsMWthreshold = 1;
            ActiveCURRENTScanGridsMWthreshold = 16;
            PassiveCURRENTScanGridsMWthreshold = 1;
            DelayInSecondsActiveScan = 60;
            RequireStationaryWhileActiveScanning = false;
            AllEnabledRadarsVisible = true;
            CharacterDetectionRange = 3000;
            ModeSwitchAtBroadcastRange = 10000;
            GpsOffSetDistanceMin = 2000;
            GpsOffSetDistanceMax = 3000;
            EnableSmallRadar = false;
            AllowSonar = false;
            SaveGpsOnActiveScan = true;
            OutputToLCD = true;
            OmitOfflineEntities = false;
            OmitOfflineEntitiesFromSonarMode = false;
        }

        public static Settings LoadConfig()
        {

            Settings defaultconfig = new Settings();
            Settings config = null;

            if (MyAPIGateway.Utilities.FileExistsInWorldStorage("Radar_Settings.xml", typeof(Settings)) == true)
            {

                try
                {

                    var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage("Radar_Settings.xml", typeof(Settings));
                    string configcontents = reader.ReadToEnd();
                    config = MyAPIGateway.Utilities.SerializeFromXML<Settings>(configcontents);

                    if (defaultconfig.settingsVersion == config.settingsVersion)
                    {

                        return config;

                    }

                }
                catch (Exception ex)
                {

                    VRage.Utils.MyLog.Default.WriteLineAndConsole($"RADAR: Error accessing Settings XML! Using Default Settings\n {ex.ToString()}");
                    return defaultconfig;
                }

                try
                {

                    defaultconfig.settingsVersion = defaultconfig.settingsVersion;
                    defaultconfig.UseMaxOutput = config.UseMaxOutput;
                    defaultconfig.ActiveMAXScanGridsMWthreshold = config.ActiveMAXScanGridsMWthreshold;
                    defaultconfig.PassiveMAXScanGridsMWthreshold = config.PassiveMAXScanGridsMWthreshold;
                    defaultconfig.ActiveCURRENTScanGridsMWthreshold = config.ActiveCURRENTScanGridsMWthreshold;
                    defaultconfig.PassiveCURRENTScanGridsMWthreshold = config.PassiveCURRENTScanGridsMWthreshold;
                    defaultconfig.DelayInSecondsActiveScan = config.DelayInSecondsActiveScan;
                    defaultconfig.RequireStationaryWhileActiveScanning = false;
                    defaultconfig.AllEnabledRadarsVisible = config.AllEnabledRadarsVisible;
                    defaultconfig.CharacterDetectionRange = config.CharacterDetectionRange;
                    defaultconfig.ModeSwitchAtBroadcastRange = config.ModeSwitchAtBroadcastRange;
                    defaultconfig.EnableSmallRadar = config.EnableSmallRadar;
                    defaultconfig.AllowSonar = config.AllowSonar;
                    defaultconfig.SaveGpsOnActiveScan = config.SaveGpsOnActiveScan;
                    defaultconfig.OutputToLCD = config.OutputToLCD;
                    defaultconfig.OmitOfflineEntities = config.OmitOfflineEntities;
                    defaultconfig.OmitOfflineEntitiesFromSonarMode = config.OmitOfflineEntitiesFromSonarMode;
                    defaultconfig.GpsOffSetDistanceMin = config.GpsOffSetDistanceMin;
                    defaultconfig.GpsOffSetDistanceMax = config.GpsOffSetDistanceMax;

                }
                catch (Exception ex)
                {

                    VRage.Utils.MyLog.Default.WriteLineAndConsole($"RADAR: Error accessing Settings XML! Using Default Settings\n {ex.ToString()}");
                    return defaultconfig;
                }

                RadarCore.OverwriteSettings = true;
                return defaultconfig;

            }

            Settings newdefaultconfig = new Settings();

            using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage("Radar_Settings.xml", typeof(Settings)))
            {

                writer.Write(MyAPIGateway.Utilities.SerializeToXML<Settings>(newdefaultconfig));

            }

            return newdefaultconfig;

        }

        public static void SaveConfig(Settings config)
        {
            try
            {
                using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage("Radar_Settings.xml", typeof(Settings)))
                {

                    writer.Write(MyAPIGateway.Utilities.SerializeToXML<Settings>(config));

                }

                RadarCore.OverwriteSettings = false;
            }
            catch (Exception ex)
            {

                VRage.Utils.MyLog.Default.WriteLineAndConsole($"RADAR: Error trying to save settings!\n {ex.ToString()}");
            }
        }

        public static Settings LoadClientConfig(Settings serverConfig)
        {
            return serverConfig;
        }
    }
}