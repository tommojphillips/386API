using UnityEngine;
using MSCLoader;
using HutongGames.PlayMaker;
using System.Collections.Generic;
using System.Collections;
using System;

namespace I386API;

/// <summary>
/// The I386 PC
/// </summary>
public class I386 {

    /// <summary>
    /// I386 instance
    /// </summary>
    public static I386 i386 { get; internal set; }

    /// <summary>
    /// I386 game object
    /// </summary>
    public static GameObject GameObject => i386.gameObject;
    /// <summary>
    /// I386 transform
    /// </summary>
    public static Transform Transform => i386.transform;

    /// <summary>
    /// The Dial Up speed in bits per second
    /// </summary>
    public static float Baud { 
        get => i386.baudFloat.Value;
        set => i386.baudFloat.Value = value;
    }

    /// <summary>
    /// Is Modem connected to telephone landline
    /// </summary>
    public static bool ModemConnected => i386.modemConnected() && i386.phonePaid();

    /// <summary>
    /// Is Modem cord connected to outlet
    /// </summary>
    public static bool ModemCord => i386.modemConnected();

    /// <summary>
    /// Is phone bill payed
    /// </summary>
    public static bool PhoneBillPaid => i386.phonePaid();

    /// <summary>
    /// Is the player using/controlling the PC
    /// </summary>
    public static bool PlayerControl => i386.playerComputer.Value;

    /// <summary>
    /// The command arguments
    /// </summary>
    public static string[] Args { get; internal set; }

    /// <summary>
    /// On turn PC Off event
    /// </summary>
    public static event Action OnTurnOff;
    /// <summary> 
    /// On turn PC On event
    /// </summary>
    public static event Action OnTurnOn;

    /// <summary>
    /// The PC Case
    /// </summary>
    public static GameObject Case => i386.pcCase;
    /// <summary>
    /// The PC Monitor
    /// </summary>
    public static GameObject Monitor => i386.pcMonitor;
    /// <summary>
    /// The PC Peripherals
    /// </summary>
    public static GameObject Peripherals => i386.pcPeripherals;
    /// <summary>
    /// The PC Speakers
    /// </summary>
    public static GameObject Speakers => i386.pcSpeakers;

    internal GameObject gameObject;
    internal Transform transform;

    internal PlayMakerFSM commandFsm;
    internal FsmString saveFile;
    internal Texture2D floppyBlankTexture;
    internal Material floppyBlankMaterial;
    internal GameObject floppyPrefab;

    internal Command currentCommand;
    internal Dictionary<string, Command> commands;
    internal Dictionary<string, Command> builtInCommands;
    internal List<BBS_Command> bbsCommands;
    internal int bbsCommandsIndex;

    internal Transform pos;
    internal TextMesh consoleText;
    internal FsmFloat baudFloat;
    internal FsmString exeString;
    internal FsmString commandString;
    internal FsmString oldString;
    internal FsmString textString;
    internal FsmString errorString;
    internal FsmBool playerComputer;
    
    internal TextMesh bbsText;
    internal FsmString bbs_command;
    internal FsmString bbs_downloadFileName;
    internal FsmFloat bbs_downloadFileSize;
    internal FsmString bbs_mode;
    internal FsmString bbs_chat_mode;
    internal PlayMakerFSM bbs_download;
    internal TextMesh bbs_downloadStatusTextMesh;

    internal Queue<char> charBuffer;
    internal Queue<KeyCode> keyBuffer;
    internal KeyCode[] keys;

    internal TextMesh bootSequenceTextMesh;

    internal GameObject pcCase;
    internal GameObject pcMonitor;
    internal GameObject pcPeripherals;
    internal GameObject pcSpeakers;

    internal struct InstallPoint {
        internal FsmInt assemblyID;
        internal FsmBool installed;
        internal FsmBool modemCord;
        internal FsmBool phonePaid;
    }

    internal List<InstallPoint> installPoints;

    internal I386() {
        commands = new Dictionary<string, Command>();
        builtInCommands = new Dictionary<string, Command>();
        bbsCommands = new List<BBS_Command>();

        charBuffer = new Queue<char>();
        keyBuffer = new Queue<KeyCode>();
        keys = (KeyCode[])Enum.GetValues(typeof(KeyCode));

        gameObject = GameObject.Find("COMPUTER");
        transform = gameObject.transform;

        OnTurnOff = null;
        OnTurnOn = null;

        // Custom command setup

        pos = transform.Find("SYSTEM/POS");
        Transform commandTransform = transform.Find("SYSTEM/POS/Command");
        commandFsm = commandTransform.GetPlayMaker("Typer");

        // add hook; add transition
        FsmState softwareListState = commandFsm.GetState("Software list");
        softwareListState.AddTransition("CUSTOM", "CustomCommand");
        commandFsm.FsmInject("Software list", onFindCommand, false);

        // create new state
        FsmState customCommandState = commandFsm.AddState("CustomCommand");
        customCommandState.AddTransition("CLOSE", "State 2");
        customCommandState.AddTransition("FINISHED", "Player input");
        commandFsm.FsmInject("CustomCommand", onCustomCommandEnter, false);
        commandFsm.FsmInject("CustomCommand", onCustomCommandUpdate, true);

        // add hook; add transition
        FsmState driveMemState = commandFsm.GetState("Drive mem");
        driveMemState.AddTransition("CUSTOM", "Player input");
        commandFsm.FsmInject("Drive mem", onGetCommandArgs, false, 1);

        // add hook; add transition
        FsmState diskMemState = commandFsm.GetState("Disk mem");
        diskMemState.AddTransition("CUSTOM", "Player input");
        commandFsm.FsmInject("Disk mem", onGetCommandArgs, false, 2);

        // Builtin custom commands setup (like dir, copy, a:, c:, etc)

        // add hook; add transition to custom
        FsmState otherCommandsState = commandFsm.GetState("Other commands?");
        commandFsm.FsmInject("Other commands?", onFindBuiltInCommand, false);
        otherCommandsState.AddTransition("CUSTOM", "CustomBuiltInCommand");

        // create new state
        FsmState customBuiltInCommandState = commandFsm.AddState("CustomBuiltInCommand");
        customBuiltInCommandState.AddTransition("CLOSE", "State 2");
        customBuiltInCommandState.AddTransition("FINISHED", "Player input");
        commandFsm.FsmInject("CustomBuiltInCommand", onCustomCommandEnter, false);
        commandFsm.FsmInject("CustomBuiltInCommand", onCustomCommandUpdate, true);

        // Add command empty check

        commandFsm.FsmInject("Init", onInit, false, 4);

        // Save file stuff
        saveFile = new FsmString("i386");
        saveFile.Value = "i386.txt";

        // Modem setup

        installPoints = new List<InstallPoint>();
        GameObject PCPartsDB = GameObject.Find("Database/AssemblyDBOthers");
        if (PCPartsDB != null) {
            PlayMakerArrayListProxy caseInstallPoints = PCPartsDB.GetArrayListProxy("386Case");
            if (caseInstallPoints != null) {
                for (int i = 0; i < caseInstallPoints._arrayList.Count; ++i) {
                    GameObject pcCase = (GameObject)caseInstallPoints._arrayList[i];
                    if (pcCase == null) {
                        continue;
                    }

                    PlayMakerFSM data = pcCase.GetPlayMaker("Data");
                    InstallPoint installPoint = new InstallPoint();
                    installPoint.assemblyID = i;
                    installPoint.installed = data.GetVariable<FsmBool>("Installed");
                    if (i == 1) {
                        // Apartment
                        GameObject modemCord_go = GameObject.Find("HOMENEW/Functions/FunctionsDisable/Telephone/Cord");
                        PlayMakerFSM modemCord_fsm = modemCord_go.GetPlayMaker("Use");
                        installPoint.modemCord = modemCord_fsm.GetVariable<FsmBool>("CordModem");

                        GameObject phoneBill_go = GameObject.Find("Systems/PhoneBills2");
                        PlayMakerFSM phoneBill_fsm = phoneBill_go.GetPlayMaker("Data");
                        installPoint.phonePaid = phoneBill_fsm.GetVariable<FsmBool>("PhonePaid");
                    }
                    else if (i == 2) {
                        // Parents house
                        GameObject modemCord_go = GameObject.Find("YARD/Building/LIVINGROOM/Telephone 1/Cord");
                        PlayMakerFSM modemCord_fsm = modemCord_go.GetPlayMaker("Use");
                        installPoint.modemCord = modemCord_fsm.GetVariable<FsmBool>("CordModem");

                        GameObject phoneBill_go = GameObject.Find("Systems/PhoneBills1");
                        PlayMakerFSM phoneBill_fsm = phoneBill_go.GetPlayMaker("Data");
                        installPoint.phonePaid = phoneBill_fsm.GetVariable<FsmBool>("PhonePaid");
                    }
                    else {
                        ModConsole.Error($"[386API] Error: Unknown assemblyID: {i}");
                        return;
                    }

                    installPoints.Add(installPoint);
                }
            }
            else {
                ModConsole.Error("[386API] Error: Database/AssemblyDBOthers.386Case not found");
                return;
            }
        }
        else {
            ModConsole.Error("[386API] Error: Database/AssemblyDBOthers not found");
            return;
        }

        GameObject fleaMarketProducts = GameObject.Find("FleaMarketProducts");
        if (fleaMarketProducts != null) {
            PlayMakerArrayListProxy fleaMarketItems = fleaMarketProducts.GetArrayListProxy("Items");
            if (fleaMarketItems != null && fleaMarketItems._arrayList.Count >= 4) {
                pcCase = (GameObject)fleaMarketItems._arrayList[0];
                pcSpeakers = (GameObject)fleaMarketItems._arrayList[1];
                pcPeripherals = (GameObject)fleaMarketItems._arrayList[2];
                pcMonitor = (GameObject)fleaMarketItems._arrayList[3];
            }
            else {
                ModConsole.Error("[386API] Error: FleaMarketItems not found");
                return;
            }
        }
        else {
            ModConsole.Error("[386API] Error: FleaMarketProducts not found");
            return;
        }

        if (pcCase == null) {
            ModConsole.Error("[386API] Error: FleaMarketItems.386case not found");
            return;
        }
        if (pcMonitor == null) {
            ModConsole.Error("[386API] Error: FleaMarketItems.pcMonitor not found");
            return;
        }
        if (pcPeripherals == null) {
            ModConsole.Error("[386API] Error: FleaMarketItems.pcPeripherals not found");
            return;
        }
        if (pcSpeakers == null) {
            ModConsole.Error("[386API] Error: FleaMarketItems.pcSpeakers not found");
            return;
        }

        // Off/On button hook
        Transform offButton = pcCase.transform.Find("Functions/ButtonPower");
        PlayMakerFSM offButton_fsm = offButton.GetPlayMaker("Use");
        offButton_fsm.FsmInject("OFF", onTurnOff, false, 2, false);
        offButton_fsm.FsmInject("OFF 2", onTurnOff, false, 2, false);
        offButton_fsm.FsmInject("ON", onTurnOn, false, 1, false);
        offButton_fsm.FsmInject("ON 2", onTurnOn, false, 1, false);

        // Reset button hook
        Transform resetButton = pcCase.transform.Find("Functions/ButtonReset");
        PlayMakerFSM resetButton_fsm = resetButton.GetPlayMaker("Use");
        resetButton_fsm.FsmInject("State 1", onTurnOff, false, 1, false);

        // PC vars

        playerComputer = PlayMakerGlobals.Instance.Variables.FindFsmBool("PlayerComputer");
        exeString = commandFsm.GetVariable<FsmString>("EXE");
        commandString = commandFsm.GetVariable<FsmString>("Command");
        errorString = commandFsm.GetVariable<FsmString>("Error");
        textString = commandFsm.GetVariable<FsmString>("Text");
        oldString = commandFsm.GetVariable<FsmString>("Old");
        baudFloat = commandFsm.GetVariable<FsmFloat>("Baud");
        consoleText = commandTransform.GetComponent<TextMesh>();

        // Floppy setup

        floppyBlankTexture = new Texture2D(128, 128);
        floppyBlankTexture.LoadImage(Properties.Resources.FLOPPY_BLANK);
        floppyBlankTexture.name = "FLOPPY_IMAGE";

        floppyPrefab = GameObject.Find("diskette(itemx)");
        MeshRenderer renderer = floppyPrefab.transform.Find("mesh").GetComponent<MeshRenderer>();
        Material[] mats = renderer.materials;
        floppyBlankMaterial = new Material(mats[1]);
        floppyBlankMaterial.name = "FLOPPY_IMAGE";
        floppyBlankMaterial.mainTexture = floppyBlankTexture;

        // TELEBBS Setup; Create custom bbs programs

        Transform telebbs = transform.Find("SYSTEM/TELEBBS");
        Transform conline = telebbs.Find("CONLINE");
        Transform files = conline.Find("FILES");

        Transform game0 = files.Find("game");
        Transform game1 = files.Find("game 1");
        Transform game2 = files.Find("game 2");
        Transform game3 = files.Find("game 3");
        Transform game4 = files.Find("game 4");
        Transform game5 = files.Find("game 5");
        Transform game6 = files.Find("game 6");
        Transform game7 = files.Find("game 7");
        Transform game8 = files.Find("game 8");
        Transform game9 = files.Find("game 9");
        Transform game10 = files.Find("game 10");

        // move all existing games on bbs to the left; making room for custom bbs programs
        if (game0) game0.localPosition = new Vector3(-6f, game0.localPosition.y, 11.57f);
        if (game1) game1.localPosition = new Vector3(-6f, game1.localPosition.y, 11.57f);
        if (game2) game2.localPosition = new Vector3(-6f, game2.localPosition.y, 11.57f);
        if (game3) game3.localPosition = new Vector3(-6f, game3.localPosition.y, 11.57f);
        if (game4) game4.localPosition = new Vector3(-6f, game4.localPosition.y, 11.57f);
        if (game5) game5.localPosition = new Vector3(-6f, game5.localPosition.y, 11.57f);
        if (game6) game6.localPosition = new Vector3(-6f, game6.localPosition.y, 11.57f);
        if (game7) game7.localPosition = new Vector3(-6f, game7.localPosition.y, 11.57f);
        if (game8) game8.localPosition = new Vector3(-6f, game8.localPosition.y, 11.57f);
        if (game9) game9.localPosition = new Vector3(-6f, game9.localPosition.y, 11.57f);
        if (game10) game10.localPosition = new Vector3(-6f, game10.localPosition.y, 11.57f);
        
        // create new text for custom bbs programs
        GameObject cGame = GameObject.Instantiate(game0.gameObject);
        cGame.name = "programs";
        cGame.transform.SetParent(files);
        cGame.transform.localPosition = new Vector3(0f, 2.52f, 11.57f);
        cGame.transform.localEulerAngles = Vector3.zero;
        bbsText = cGame.GetComponent<TextMesh>();
        bbsText.text = "";
        bbsText.anchor = TextAnchor.UpperLeft;
        bbsText.lineSpacing = 0.87f;

        Transform initialize = conline.Find("Initialize");

        PlayMakerFSM type = initialize.GetPlayMaker("Type");
        type.InitializeFSM();
        type.FsmInject("Player input 2", onBBSBrowseFiles, true);
        bbs_mode = type.GetVariable<FsmString>("Mode");

        // fix mode not being set when in chat mode and changing modes to files/ranks.
        Transform chat = conline.Find("CHAT");
        PlayMakerFSM chat_type = chat.GetPlayMaker("Type");
        chat_type.FsmInject("Check mode", onBBSFixChat, false, 4);
        bbs_chat_mode = chat_type.GetVariable<FsmString>("Mode");

        // fix telebbs text disappearing
        Transform bbs_bg1 = telebbs.Find("Software/bg 1");
        bbs_bg1.localPosition = new Vector3(0f, -7.05f, 2.0f);
        bbs_bg1.localEulerAngles = Vector3.zero;
        bbs_bg1.localScale = new Vector3(20f, 0.6f, 0.54f);

        bbs_download = initialize.GetPlayMaker("Download");
        bbs_download.InitializeFSM();
        FsmState state1 = bbs_download.GetState("State 1");
        state1.AddTransition("CUSTOM", "State 3"); // add transition to state3 - enable DownloadStatus
        bbs_download.FsmInject("State 1", onCheckBBSCommand, false);
        bbs_command = bbs_download.GetVariable<FsmString>("Command");

        Transform status = conline.Find("DownloadStatus");
        PlayMakerFSM downloadStatus = status.GetPlayMaker("Data");
        downloadStatus.InitializeFSM();
        bbs_downloadFileName = downloadStatus.GetVariable<FsmString>("FileName");
        bbs_downloadFileSize = downloadStatus.GetVariable<FsmFloat>("FileSize");
        bbs_downloadStatusTextMesh = status.GetComponent<TextMesh>();

        // boot sequence text
        Transform bootSequence = pos.Find("Text");
        bootSequenceTextMesh = bootSequence.GetComponent<TextMesh>();
    }

    /// <summary>
    /// Get the time it would take to download x bytes at current baud speed.
    /// </summary>
    /// <param name="bytes">Number of bytes</param>
    public static float GetDownloadTime(int bytes) {
        return bytes / GetBps();
    }
    /// <summary>
    /// Get Dial Up speed in Bytes per second
    /// </summary>
    public static float GetBps() {
        // 10 bits per byte (8N1)
        return i386.baudFloat.Value / 10f;
    }

    /// <summary>
    /// POS Write new line to console
    /// </summary>
    public static void POS_NewLine() {
        i386.pos.localPosition = Vector3.zero;
        i386.oldString.Value = i386.textString.Value + "\n";
        i386.textString.Value = i386.oldString.Value;
        i386.consoleText.text = i386.oldString.Value;
    }
    /// <summary>
    /// POS Write message with new line to console
    /// </summary>
    /// <param name="text">The text to write</param>
    public static void POS_WriteNewLine(string text) {
        i386.pos.localPosition = Vector3.zero;
        i386.oldString.Value = i386.textString.Value + text + "\n";
        i386.commandString.Value = string.Empty;
        i386.errorString.Value = string.Empty;
        i386.textString.Value = i386.oldString.Value;
        i386.consoleText.text = i386.oldString.Value;
    }
    /// <summary>
    /// POS Write message to console
    /// </summary>
    /// <param name="text">The text to write</param>
    public static void POS_Write(string text) {
        i386.pos.localPosition = Vector3.zero;
        i386.oldString.Value = i386.textString.Value + text;
        i386.commandString.Value = string.Empty;
        i386.errorString.Value = string.Empty;
        i386.textString.Value = i386.oldString.Value;
        i386.consoleText.text = i386.oldString.Value;
    }
    /// <summary>
    /// POS Clear Screen
    /// </summary>
    public static void POS_ClearScreen() {
        i386.pos.localPosition = Vector3.zero;
        i386.commandString.Value = string.Empty;
        i386.errorString.Value = string.Empty;
        i386.oldString.Value = "\n";
        i386.textString.Value = string.Empty;
        i386.consoleText.text = i386.oldString.Value;
    }
    /// <summary>
    /// POS Get Char. returns char if pressed otherwise returns '\0'
    /// </summary>
    public static char POS_GetChar() {
        if (!i386.playerComputer.Value || i386.charBuffer.Count == 0) {
            return '\0';
        }

        return i386.charBuffer.Dequeue();
    }
    /// <summary>
    /// POS Clear the character buffer
    /// </summary>
    public static void POS_ClearCharBuffer() {
        i386.charBuffer.Clear();
    }
    /// <summary>
    /// POS Get Key Code. returns pressed keycode
    /// </summary>
    public static KeyCode POS_GetKeyCode() {
        if (!i386.playerComputer.Value || i386.keyBuffer.Count == 0) {
            return KeyCode.None;
        }

        return i386.keyBuffer.Dequeue();
    }
    /// <summary>
    /// POS Clear the keycode buffer
    /// </summary>
    public static void POS_ClearKeyBuffer() {
        i386.keyBuffer.Clear();
    }

    /// <summary>
    /// Get key
    /// </summary>
    /// <param name="key">key code</param>
    public static bool GetKey(KeyCode key) {
        return i386.playerComputer.Value && Input.GetKey(key);
    }
    /// <summary>
    /// Get key down
    /// </summary>
    /// <param name="key">key code</param>
    public static bool GetKeyDown(KeyCode key) {
        return i386.playerComputer.Value && Input.GetKeyDown(key);
    }
    /// <summary>
    /// Get key up
    /// </summary>
    /// <param name="key">key code</param>
    public static bool GetKeyUp(KeyCode key) {
        return i386.playerComputer.Value && Input.GetKeyUp(key);
    }
    
    /// <summary>
    /// Start a coroutine
    /// </summary>
    public static Coroutine StartCoroutine(IEnumerator enumerator) {
        return i386.commandFsm.StartCoroutine(enumerator);
    }
    /// <summary>
    /// Stop a coroutine
    /// </summary>
    public static void StopCoroutine(Coroutine coroutine) {
        i386.commandFsm.StopCoroutine(coroutine);
    }
    /// <summary>
    /// Stop all coroutines
    /// </summary>
    public static void StopAllCoroutines() {
        i386.commandFsm.StopAllCoroutines();
    }

    private void exitCommand() {
        commandFsm.SendEvent("FINISHED");
        currentCommand = null;
        clearInput();
        StopAllCoroutines();
    }
    private char keyToChar(KeyCode key) {
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (key >= KeyCode.A && key <= KeyCode.Z) {
            char c = (char)('a' + (key - KeyCode.A));
            return shift ? Char.ToUpper(c) : c; 
        }

        if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) {
            string normal = "0123456789";
            string shifted = ")!@#$%^&*(";
            int i = key - KeyCode.Alpha0;
            return shift ? shifted[i] : normal[i];
        }

        if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9) {
            return (char)('0' + (key - KeyCode.Keypad0));
        }

        switch (key) {
            case KeyCode.Space:
                return ' ';
            case KeyCode.Return:
                return '\n';
            case KeyCode.Backspace:
                return '\b';
            case KeyCode.Tab:
                return '\t';
            case KeyCode.Period:
                return shift ? '>' : '.';
            case KeyCode.Comma:
                return shift ? '<' : ',';
            case KeyCode.Minus:
                return shift ? '_' : '-';
            case KeyCode.Equals:
                return shift ? '+' : '=';
            case KeyCode.Semicolon: 
                return shift ? ':' : ';';
            case KeyCode.Quote:
                return shift ? '"' : '\'';
            case KeyCode.Slash:
                return shift ? '?' : '/';
            case KeyCode.Backslash:
                return shift ? '|' : '\\';
            case KeyCode.LeftBracket: 
                return shift ? '{' : '[';
            case KeyCode.RightBracket: 
                return shift ? '}' : ']';
            case KeyCode.BackQuote: 
                return shift ? '~' : '`';
            case KeyCode.KeypadPeriod:
                return '.';
            case KeyCode.KeypadPlus:
                return '+';
            case KeyCode.KeypadMinus:
                return '-';
            case KeyCode.KeypadMultiply:
                return '*';
            case KeyCode.KeypadDivide:
                return '/';
            case KeyCode.KeypadEnter:
                return '\n';
        }

        return '\0';
    }
    private void updateInput() {
        if (!i386.playerComputer.Value) {
            return;
        }

        foreach (KeyCode key in keys) {
            if (!Input.GetKeyDown(key)) {
                continue;
            }

            if (key != KeyCode.None) {
                keyBuffer.Enqueue(key);
            }

            char c = keyToChar(key);
            if (c != '\0') {
                charBuffer.Enqueue(c);
            }
        }
    }
    private void clearInput() {
        charBuffer.Clear();
        keyBuffer.Clear();
    }
    private void clearBootText() {
        bootSequenceTextMesh.text = "";
    }
    private void setBootText(string message) {
        bootSequenceTextMesh.text = message;
    }
    private bool phonePaid() {
        for (int i = 0; i < installPoints.Count; ++i) {
            if (installPoints[i].installed.Value) {
                return installPoints[i].phonePaid.Value;
            }
        }
        return false;
    }
    private bool modemConnected() {
        for (int i = 0; i < installPoints.Count; ++i) {
            if (installPoints[i].installed.Value) {
                return installPoints[i].modemCord.Value;
            }
        }
        return false;
    }

    // Callbacks/Events

    private void onInit() {
        POS_NewLine();
    }
    private void onGetCommandArgs() {
        // new line on empty command (dont display error msg if command empty)
        if (commandString.Value == string.Empty) {
            commandFsm.SendEvent("CUSTOM");
            return;
        }

        // implement support for command arguments
        Args = commandString.Value.Split(' ');
        if (Args.Length > 0) {
            exeString.Value = Args[0];
        }
        else {
            exeString.Value = commandString.Value;
        }
    }
    private void onFindCommand() {
        // find custom command
        if (commands.TryGetValue(exeString.Value, out currentCommand)) {
            // start custom command
            commandFsm.SendEvent("CUSTOM");
        }
    }
    private void onCustomCommandUpdate() {
        updateInput();

        bool t = true;
        if (currentCommand?.OnUpdate != null) {
            t = currentCommand.OnUpdate.Invoke();
        }

        if (t) {
            exitCommand();
        }
    }
    private void onCustomCommandEnter() {
        clearInput();
        clearBootText();

        bool t = false;
        if (currentCommand?.OnEnter != null) {
            t = currentCommand.OnEnter.Invoke();
        }
        
        if (t) {
            exitCommand();
        }
    }

    private void onFindBuiltInCommand() {
        // find builtin custom command
        if (builtInCommands.TryGetValue(exeString.Value, out currentCommand)) {
            // start builtin custom command
            commandFsm.SendEvent("CUSTOM");
        }
    }

    private void onPopulateBBS() {
        bbsText.text = "";
        for (int i = bbsCommandsIndex; i < i386.bbsCommands.Count; i++) {
            if (i - bbsCommandsIndex >= 11) {
                break; // only room for 11 entries
            }
            bbsText.text += $"* {i386.bbsCommands[i].exe}.exe * {i386.bbsCommands[i].size}\n";
        }
    }
    private void onCheckBBSCommand() {

        for (int i = 0; i < bbsCommands.Count; i++) {
            string command = bbs_command.Value;

            if (bbs_command.Value == $"/load {bbsCommands[i].exe}.exe c:") {
                bbs_downloadFileName.Value = bbsCommands[i].exe;
                bbs_downloadFileSize.Value = bbsCommands[i].size;
                bbs_downloadStatusTextMesh.text = "0.00";
                bbs_download.SendEvent("CUSTOM");
                break;
            }
        }
    }
    private void onBBSBrowseFiles() {
        // this is only called when in bbs and the player is at PC.

        if (bbs_mode.Value != "/F") {
            return;
        }

        onPopulateBBS();
        
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.mouseScrollDelta.y > 0) {
            if (bbsCommandsIndex > 0) {
                bbsCommandsIndex--;
            }
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.mouseScrollDelta.y < 0) {
            if (bbsCommandsIndex < bbsCommands.Count && bbsCommands.Count - bbsCommandsIndex > 11) {
                bbsCommandsIndex++;
            }
        }
    }
    private void onBBSFixChat() {
        // this is only called when in bbs in chat
        // fix bbs mode not being set when in chat mode and changing mode to /r or /f
        bbs_mode.Value = bbs_chat_mode.Value;
    }
    private void onTurnOff() {
        if (currentCommand != null) {
            exitCommand();
        }
        OnTurnOff?.Invoke();
    }
    private void onTurnOn() {
        OnTurnOn?.Invoke();
    }
}
