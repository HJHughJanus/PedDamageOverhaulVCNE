using System.Reflection;
using System;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;
using IVSDKDotNet;
using IVSDKDotNet.Attributes;
using IVSDKDotNet.Enums;
using static IVSDKDotNet.Native.Natives;
using CCL.GTAIV;
using System.Numerics;
using IVSDKDotNet.Native;
using System.Drawing;
//using System.Reflection.Metadata;

namespace PedDamageOverhaulVCNE
{
    class PedClass
    {
        public int iCombatDeathCounter, iFireDeathCounter;
        public bool bAllowedToDie;
    }
    public class PDOVCNE : Script
    {
        public bool SetKey(int toKey, out Keys key)
        {
            switch (toKey)
            {
                case 1:
                    key = Keys.F1;
                    return true;
                case 2:
                    key = Keys.F2;
                    return true;
                case 3:
                    key = Keys.F3;
                    return true;
                case 4:
                    key = Keys.F4;
                    return true;
                case 5:
                    key = Keys.F5;
                    return true;
                case 6:
                    key = Keys.F6;
                    return true;
                case 7:
                    key = Keys.F7;
                    return true;
                case 8:
                    key = Keys.F8;
                    return true;
                case 9:
                    key = Keys.F9;
                    return true;
                case 10:
                    key = Keys.F10;
                    return true;
                case 11:
                    key = Keys.F11;
                    return true;
                case 12:
                    key = Keys.F12;
                    return true;
                default:
                    key = Keys.None;
                    return false;
            }
        }
        public bool GetIniFile(string path, out Dictionary<string, string> dict)
        {
            if (File.Exists(path))
            {
                dict = new Dictionary<string, string>();
                foreach (string line in File.ReadLines(path))
                {
                    if (!(line[0].Equals(";") || line[0].Equals("[") || line[0].Equals(" ")))
                    {
                        var keyValue = line.Split(new[] { '=' }, 2);
                        if (keyValue.Length == 2)
                        {
                            dict.Add(keyValue[0], keyValue[1]);
                        }
                    }
                }
                return true;
            }
            else
            {
                dict = new Dictionary<string, string>();
                return false;
            }
        }

        public bool GetTargetedPed(out NativePed target)
        {
            IVPool pedPool = IVPools.GetPedPool();
            bool bPlayerFound = false;
            int iPlayerHandle = 0;
            int iCounter = 0;

            while (!bPlayerFound)
            {
                if (iCounter < pedPool.Count)
                {
                    UIntPtr ptr = pedPool.Get(iCounter);
                    if (ptr != UIntPtr.Zero) continue;
                    IVPed ped = IVPed.FromUIntPtr(ptr);
                    if (ped.IsPlayer)
                    {
                        iPlayerHandle = (int)pedPool.GetIndex(ptr);
                        bPlayerFound = true;
                    }
                    iCounter++;
                }
            }

            for (int i = 0; i < pedPool.Count; i++)
            {
                UIntPtr ptr = pedPool.Get(i);
                if (ptr != UIntPtr.Zero) continue;
                IVPed ped = IVPed.FromUIntPtr(ptr);
                if (!ped.IsPlayer)
                {
                    int handle = (int)pedPool.GetIndex(ptr);
                    if (IS_PLAYER_FREE_AIMING_AT_CHAR(iPlayerHandle, handle))
                    {
                        target = new NativePed(handle);
                        return true;
                    }
                }
            }
            target = null;
            return false;
        }

        bool bPDOEnabled = true, bPDODisabledDuringMissions = false, bIniFound = false, bShowNPCInfo = false, bLastTarget_IsAlive, bLastTarget_IsInjured, bLastTarget_IsRagdoll;
        Dictionary<NativePed, PedClass> dPedMap = new Dictionary<NativePed, PedClass>();
        Dictionary<string, string> dPDOIni;
        List<NativePed> lPedsToRemove = new List<NativePed>();
        int iIntervalValue = 250, iUpperHealthThreshold = -45, iLowerHealthThreshold = -80, iMaxCombatDeaths = 10, iMaxFireDeaths = 17, iLoopsDone = 0, iClearDictAfterLoopsDone = 100, iClearingsDone = 0, iLastTarget_Health;
        Keys kPDOToggleKey = Keys.F9, kShowNPCInfoToggleKey = Keys.F8;
        NativePed pLastTarget;
        string sTempIniValue;
        Vector2 vec2TextPos = new Vector2(500, 500);
        Color colTextColor = Color.White;
        float fTextSize = 1;

        public PDOVCNE()
        {
            var workingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase).Substring(6);
            var IniFileName = "PedDamageOverhaulVCNE.ivsdk.ini";
            var IniPath = $@"{workingDirectory}\{IniFileName}";
            if (File.Exists(IniPath))
            {
                if (GetIniFile(IniPath, out dPDOIni))
                {
                    bIniFound = true;
                    if (dPDOIni.TryGetValue("bEnablePDO", out sTempIniValue))
                    {
                        bPDOEnabled = Boolean.Parse(sTempIniValue);
                    }
                    if (dPDOIni.TryGetValue("iUpperHealthThreshold", out sTempIniValue))
                    {
                        iUpperHealthThreshold = Int32.Parse(sTempIniValue);
                    }
                    if (dPDOIni.TryGetValue("iLowerHealthThreshold", out sTempIniValue))
                    {
                        iLowerHealthThreshold = Int32.Parse(sTempIniValue);
                    }
                    if (dPDOIni.TryGetValue("iMaxCombatHealthResets", out sTempIniValue))
                    {
                        iMaxCombatDeaths = Int32.Parse(sTempIniValue);
                    }
                    if (dPDOIni.TryGetValue("iMaxFireHealthResets", out sTempIniValue))
                    {
                        iMaxFireDeaths = Int32.Parse(sTempIniValue);
                    }
                    if (dPDOIni.TryGetValue("iTickInterval", out sTempIniValue))
                    {
                        iIntervalValue = Int32.Parse(sTempIniValue);
                    }
                    if (dPDOIni.TryGetValue("iClearNPCsAfterTickIntervals", out sTempIniValue))
                    {
                        iClearDictAfterLoopsDone = Int32.Parse(sTempIniValue);
                    }
                    if (dPDOIni.TryGetValue("bDisablePDODuringMissions", out sTempIniValue))
                    {
                        bPDODisabledDuringMissions = Boolean.Parse(sTempIniValue);
                    }
                    if (dPDOIni.TryGetValue("bShowNPCInfo", out sTempIniValue))
                    {
                        bShowNPCInfo = Boolean.Parse(sTempIniValue);
                    }
                    if (dPDOIni.TryGetValue("iShowNPCInfoToggleKey", out sTempIniValue))
                    {
                        int tempKey = Int32.Parse(sTempIniValue);
                        Keys key;
                        if (SetKey(tempKey, out key))
                        {
                            kShowNPCInfoToggleKey = key;
                        }
                    }
                    if (dPDOIni.TryGetValue("iPDOToggleKey", out sTempIniValue))
                    {
                        int tempKey = Int32.Parse(sTempIniValue);
                        Keys key;
                        if (SetKey(tempKey, out key))
                        {
                            kPDOToggleKey = key;
                        }
                    }
                }
            }

            //Interval = iIntervalValue;
            //BindKey(kPDOToggleKey, new KeyPressDelegate(TogglePDO));
            //BindKey(kShowNPCInfoToggleKey, new KeyPressDelegate(ToggleShowNPCInfo));
            this.Tick += new EventHandler(this.PedDamageOverhaulVCNE_Tick);
        }

        private void PedDamageOverhaulVCNE_Tick(object sender, EventArgs e)
        {
            bool bGreenLight = true;

            if (bPDODisabledDuringMissions)
            {
                if (IVTheScripts.IsPlayerOnAMission())
                {
                    bGreenLight = false;
                }
            }

            if (bPDOEnabled && bGreenLight)
            {
                int iArraySize = 1024;
                NativePed[] aAllPeds = new NativePed[iArraySize];
                IVPool pedPool = IVPools.GetPedPool();
                NativePed playerPed = null;

                for (int i = 0; i < pedPool.Count; i++)
                {
                    // Getting the memory address of the ped in the pool
                    UIntPtr ptr = pedPool.Get(i);

                    // Check if the memory address is valid and continue if it's valid
                    if (ptr != UIntPtr.Zero) continue;

                    // Creating an IVPed instance from the memory address
                    IVPed ped = IVPed.FromUIntPtr(ptr);

                    // Getting the ped handle from the memory address which we can use to call native functions
                    int handle = (int)pedPool.GetIndex(ptr);

                    // Creating NativePed (so the comfortable functions of this class can be used)
                    NativePed p = new NativePed(handle);

                    // If the current ped is the player, set the corresponding variable accordingly
                    if (ped.IsPlayer) playerPed = p;

                    // Filling NativePed array (which is later used to affect all the Peds)
                    aAllPeds[i] = p;
                }

                for (int i = 0; i < aAllPeds.Length; i++)
                {
                    if (!dPedMap.ContainsKey(aAllPeds[i]))
                    {
                        PedClass p = new PedClass();
                        p.bAllowedToDie = false;
                        p.iCombatDeathCounter = 0;
                        p.iFireDeathCounter = 0;
                        dPedMap.Add(aAllPeds[i], p);
                    }
                }

                foreach (KeyValuePair<NativePed, PedClass> ped in dPedMap)
                {
                    if (ped.Key.Exists())
                    {
                        if (ped.Key.IsAlive && ped.Key != playerPed)
                        {
                            if (!ped.Value.bAllowedToDie)
                            {
                                if (ped.Key.IsOnFire)
                                {
                                    if (ped.Key.Health <= iLowerHealthThreshold)
                                    {
                                        ped.Key.Health = iUpperHealthThreshold;
                                        //ped.Key.IsOnFire = true;
                                        ped.Value.iFireDeathCounter++;
                                        if (ped.Value.iFireDeathCounter >= iMaxFireDeaths)
                                        {
                                            ped.Value.bAllowedToDie = true;
                                        }
                                    }
                                }
                                else
                                {
                                    if (ped.Key.Health <= iUpperHealthThreshold)
                                    {
                                        if (ped.Key.Health <= iLowerHealthThreshold)
                                        {
                                            ped.Key.Health = iUpperHealthThreshold;
                                            ped.Value.iCombatDeathCounter++;
                                            if (ped.Value.iCombatDeathCounter >= iMaxCombatDeaths)
                                            {
                                                ped.Value.bAllowedToDie = true;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        lPedsToRemove.Add(ped.Key);
                    }
                }

                if (bShowNPCInfo)
                {
                    NativePed target;

                    if (GetTargetedPed(out target))
                    {
                        if (target != null)
                        {
                            pLastTarget = target;
                        }
                        if (pLastTarget != null)
                        {
                            iLastTarget_Health = pLastTarget.Health;
                            bLastTarget_IsAlive = pLastTarget.IsAlive;
                            bLastTarget_IsInjured = pLastTarget.IsInjured;
                            bLastTarget_IsRagdoll = pLastTarget.IsRagdoll;
                        }
                        NativeDrawing.DisplayText(vec2TextPos, "NPC Health: " + iLastTarget_Health + "\nNPC IsAlive: " + bLastTarget_IsAlive + "\nNPC IsInjured: " + bLastTarget_IsInjured + "\nNPC IsRagdoll: " + bLastTarget_IsRagdoll, colTextColor, fTextSize, false);

                    }
                }

                iLoopsDone++;

                if (iLoopsDone % iClearDictAfterLoopsDone == 0)
                {
                    foreach (NativePed ped in lPedsToRemove)
                    {
                        dPedMap.Remove(ped);
                    }
                    iClearingsDone++;
                    lPedsToRemove.Clear();
                }
            }
        }

        private void ToggleShowNPCInfo()
        {
            bShowNPCInfo = !bShowNPCInfo;
        }

        private void TogglePDO()
        {
            bPDOEnabled = !bPDOEnabled;
            if (bPDOEnabled)
            {
                if (bIniFound)
                {
                    NativeDrawing.DisplayText(vec2TextPos, "PDO enabled.\nini-File found.", colTextColor, fTextSize, false);
                }
                else
                {
                    NativeDrawing.DisplayText(vec2TextPos, "PDO enabled.\nini-File not found. Default values loaded.", colTextColor, fTextSize, false);
                }
            }
            else
            {
                NativeDrawing.DisplayText(vec2TextPos, "PDO disabled!", colTextColor, fTextSize, false);
            }
        }
    }
}