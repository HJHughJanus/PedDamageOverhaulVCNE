using System.Reflection;
using System;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;
using IVSDKDotNet;
using static IVSDKDotNet.Native.Natives;
using CCL.GTAIV;
using System.Numerics;
using System.Drawing;

namespace PedDamageOverhaulVCNE
{
    #region Classes
    public class Main : Script
    {
        #region Variables
        bool bIniFileRead = false, bPDOEnabled = true, bPDODisabledDuringMissions = false, bIniFound = false, bInvincibility = false, bShowNPCInfo = false, bLastTarget_IsAlive, bLastTarget_IsInjured, bLastTarget_IsRagdoll, bLastTarget_IsAllowedToDie, bEnablingMessageDisplayed = false, bDisablingMessageDisplayed = false;
        Dictionary<int, PedClass> dPedMap = new Dictionary<int, PedClass>();
        Dictionary<string, string> dPDOIni;
        List<int> lPedsToRemove = new List<int>();
        int iIntervalValue = 1000, iMaxNPCsToAffect = 1024, iUpperHealthThreshold = 50, iLowerHealthThreshold = 30, iMaxCombatDeaths = 10, iMaxFireDeaths = 17, iLoopsDone = 0, iClearDictAfterLoopsDone = 100, iClearingsDone = 0, iLastTarget_Health, iLastTarget_SavedCounter;
        Keys kPDOToggleKey = Keys.F9, kShowNPCInfoToggleKey = Keys.F8;
        NativePed pLastTarget;
        string sTempIniValue, IniPath, starget = "None";
        #endregion

        #region Functions
        // Reads an ini file and returns a dictionary with the ini's values
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
        // Checks if a Ped is aimed at by the player and returns it, if so
        public bool GetTargetedPed(out NativePed target)
        {
            // Getting Ped Pool
            IVPool pedPool = IVPools.GetPedPool();

            // Getting the player Handle and Ped
            int playerID = CONVERT_INT_TO_PLAYERINDEX(GET_PLAYER_ID());
            GET_PLAYER_CHAR(playerID, out int pPed);
            int iPlayerHandle = pPed;
            NativePed playerPed = new NativePed(pPed);

            // Iterating through Peds and checking if player is aiming at any (if found, return the targeted Ped)
            for (int i = 0; i < pedPool.Count; i++)
            {
                // Getting the memory address of the ped in the pool
                UIntPtr ptr = pedPool.Get(i);
                // Check if the memory address is valid and continue if it's valid
                if (ptr == UIntPtr.Zero) continue;
                // Creating an IVPed instance from the memory address
                IVPed ped = IVPed.FromUIntPtr(ptr);
                // Getting the ped handle from the memory address which we can use to call native functions
                int iPedHandle = (int)pedPool.GetIndex(ptr);
                // Creating NativePed (so the comfortable functions of this class can be used)
                NativePed p = new NativePed(iPedHandle);

                // If the current Ped from the pool is not the player, check if the player is aiming at it (if so, return the Ped)
                if (p != playerPed)
                {
                    if (IS_PLAYER_TARGETTING_CHAR(playerID, iPedHandle))
                    {
                        target = p;
                        return true;
                    }
                }
            }

            // If no Ped is being aimed at, return null/false
            target = null;
            return false;
        }
        // Translates an integer input to an F-key (1 becomes F1, 2 becomes F2, etc.)
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
        // Function for toggling the bShowNPCInfo bool
        private void ToggleShowNPCInfo()
        {
            bShowNPCInfo = !bShowNPCInfo;
        }
        // Function for toggling the bPDOEnabled bool
        private void TogglePDO()
        {
            bPDOEnabled = !bPDOEnabled;
            if (bPDOEnabled) bDisablingMessageDisplayed = false;
            else
            {
                bEnablingMessageDisplayed = false;
                bIniFileRead = false;
            }
        }
        // Returns a string which is to be displayed on screen (e.g. upon toggling the mod on/off)
        private string GetPDOText(bool PDOEnabled, bool IniFound)
        {
            string sResult = "";
            if (PDOEnabled)
            {
                if (IniFound) sResult = "PDO enabled. ini-File found.";
                else sResult = "PDO enabled. ini-File not found (File was searched here: " + IniPath + "). Default values loaded.";
            }
            else
            {
                sResult = "PDO disabled!";
            }
            return sResult;
        }
        #endregion

        #region Constructor
        public Main()
        {
            if (!bIniFileRead)
            {
                // Setting up the directory for finding the ini file
                string workingDirectory = Path.GetDirectoryName(Application.ExecutablePath),
                IniFileNameAndPath = "IVSDKDotNet\\scripts\\PedDamageOverhaulVCNE.ivsdk.ini";
                IniPath = workingDirectory + "\\" + IniFileNameAndPath;

                // Check if the ini file exists - if so, read it and map its settings to the corresponding variables
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
                        if (dPDOIni.TryGetValue("iMaxNPCsToAffect", out sTempIniValue))
                        {
                            iMaxNPCsToAffect = Int32.Parse(sTempIniValue);
                        }
                        if (dPDOIni.TryGetValue("bDisablePDODuringMissions", out sTempIniValue))
                        {
                            bPDODisabledDuringMissions = Boolean.Parse(sTempIniValue);
                        }
                        if (dPDOIni.TryGetValue("bInvincibility", out sTempIniValue))
                        {
                            bInvincibility = Boolean.Parse(sTempIniValue);
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
                bIniFileRead = true;
            }
            KeyDown += Main_KeyDown;
            WAIT(iIntervalValue);
            Tick += Main_Tick;
        }
        #endregion

        private void Main_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == kPDOToggleKey) TogglePDO();
            else if (e.KeyCode == kShowNPCInfoToggleKey) ToggleShowNPCInfo();            
        }

        private void Main_Tick(object sender, EventArgs e)
        {
            bool bGreenLight = true;
            // Message when enabling PDO
            if (bPDOEnabled && !bEnablingMessageDisplayed)
            {
                string sText = "";
                sText = GetPDOText(bPDOEnabled, bIniFound);
                NativeGame.DisplayCustomHelpMessage(sText, true, false);
                bEnablingMessageDisplayed = true;
            }

            // Message when disabling PDO
            if (!bPDOEnabled && !bDisablingMessageDisplayed)
            {
                string sText = "";
                sText = GetPDOText(bPDOEnabled, bIniFound);
                NativeGame.DisplayCustomHelpMessage(sText, true, false);
                bDisablingMessageDisplayed = true;
            }

            // Check if PDO should work in missions and adjust the "green light" bool accordingly
            if (bPDODisabledDuringMissions)
            {
                if (IVTheScripts.IsPlayerOnAMission())
                {
                    bGreenLight = false;
                }
            }

            // If PDO is enabled and the "green light" was given, do the magic
            if (bPDOEnabled && bGreenLight)
            {
                // Setting up Ped Pool variables
                //int iArraySize = iMaxNPCsToAffect;
                //NativePed[] aAllPeds = new NativePed[iArraySize];
                IVPool pedPool = IVPools.GetPedPool();

                // Getting the player Ped
                int playerID = CONVERT_INT_TO_PLAYERINDEX(GET_PLAYER_ID());
                GET_PLAYER_CHAR(playerID, out int pPed);
                NativePed playerPed = new NativePed(pPed);

                // Render the player invincible, if the option was chosen
                if (bInvincibility) SET_CHAR_INVINCIBLE(playerPed.Handle, true);

                // Iterate over the Ped Pool and fill up the Ped Map (filtering out unwanted Ped Pool entries and converting wanted ones)
                for (int i = 0; i < pedPool.Count; i++)
                {
                    // Getting the memory address of the ped in the pool
                    UIntPtr ptr = pedPool.Get(i);

                    // Check if the memory address is valid and continue if it's valid
                    if (ptr == UIntPtr.Zero) continue;

                    // Creating an IVPed instance from the memory address
                    IVPed ped = IVPed.FromUIntPtr(ptr);

                    // Getting the ped handle from the memory address which we can use to call native functions
                    int handle = (int)pedPool.GetIndex(ptr);

                    // Creating NativePed (so the comfortable functions of this class can be used)
                    NativePed p = new NativePed(handle);

                    // Fill the Ped Map with the converted Ped
                    if (p != null)
                    {
                        if (!dPedMap.ContainsKey(handle))
                        {
                            PedClass pc = new PedClass();
                            pc.bAllowedToDie = false;
                            pc.iCombatDeathCounter = 0;
                            pc.iFireDeathCounter = 0;
                            pc.NativePed = p;
                            dPedMap.Add(handle, pc);
                        }
                    }
                }

                // Iterate over the Ped Map and apply PDO's effects
                foreach (KeyValuePair<int, PedClass> ped in dPedMap)
                {
                    if (ped.Value.NativePed.Exists())
                    {
                        if (ped.Value.NativePed.IsAlive && ped.Value.NativePed != playerPed)
                        {
                            if (!ped.Value.bAllowedToDie && ped.Value.NativePed.Health <= iUpperHealthThreshold)
                            {
                                
                                if (ped.Value.NativePed.Health <= iLowerHealthThreshold)
                                {
                                    ped.Value.NativePed.Health = iUpperHealthThreshold;
                                    ped.Value.iFireDeathCounter++;
                                    ped.Value.iCombatDeathCounter++;
                                    if (ped.Value.NativePed.IsOnFire)
                                    {
                                        if (ped.Value.iFireDeathCounter >= iMaxFireDeaths)
                                        {
                                            ped.Value.bAllowedToDie = true;
                                        }
                                    }
                                    else
                                    {
                                        if (ped.Value.iCombatDeathCounter >= iMaxCombatDeaths)
                                        {
                                            ped.Value.bAllowedToDie = true;
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
                
                // Show Infos about the targeted NPC, if the option was chosen
                if (bShowNPCInfo)
                {
                    NativePed target;
                    PedClass targetClass;                    
                    
                    if (GetTargetedPed(out target))
                    {
                        if (target != null)
                        {
                            pLastTarget = target;
                        }
                    }
                    if (pLastTarget != null)
                    {
                        if (dPedMap.TryGetValue(pLastTarget.Handle, out targetClass))
                        {
                            iLastTarget_SavedCounter = targetClass.iCombatDeathCounter;
                            bLastTarget_IsAllowedToDie = targetClass.bAllowedToDie;
                        }
                        iLastTarget_Health = pLastTarget.Health;
                        bLastTarget_IsAlive = pLastTarget.IsAlive;
                        bLastTarget_IsInjured = pLastTarget.IsInjured;
                        bLastTarget_IsRagdoll = pLastTarget.IsRagdoll;
                        starget = pLastTarget.Handle.ToString();
                    }
                    NativeGame.DisplayCustomHelpMessage(string.Format("Target: {0}, NPC Health: {1}, NPC AllowedToDie: {6}, NPC Saved Counter: {5}, NPC IsAlive: {2}, NPC IsInjured: {3}, NPC IsRagdoll: {4}", starget, iLastTarget_Health, bLastTarget_IsAlive, bLastTarget_IsInjured, bLastTarget_IsRagdoll, iLastTarget_SavedCounter, bLastTarget_IsAllowedToDie), true, false);
                }

                // Count the script iterations
                iLoopsDone++;

                // Clean up the Ped Map when enough script iterations have taken place
                if (iLoopsDone % iClearDictAfterLoopsDone == 0)
                {
                    foreach (int ped in lPedsToRemove)
                    {
                        dPedMap.Remove(ped);
                    }
                    iClearingsDone++;
                    lPedsToRemove.Clear();
                }
            }
        }

        }
    // A Class representing a Ped and its properties (which are needed for PDO to work)
    class PedClass
    {
        public int iCombatDeathCounter;
        public int iFireDeathCounter;
        public bool bAllowedToDie;
        public NativePed NativePed;
    }
    #endregion

}