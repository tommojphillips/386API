using UnityEngine;
using MSCLoader;
using HutongGames.PlayMaker;
using System.Collections.Generic;
using System.Collections;

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
    public GameObject gameObject;
    /// <summary>
    /// I386 transform
    /// </summary>
    public Transform transform;

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
    public static bool ModemConnected => i386.modemCord.Value && i386.phonePaid.Value;

    /// <summary>
    /// Is Modem cord connected to outlet
    /// </summary>
    public static bool ModemCord => i386.modemCord.Value;

    /// <summary>
    /// Is phone bill payed
    /// </summary>
    public static bool PhoneBillPaid => i386.phonePaid.Value;

    /// <summary>
    /// Is PC powered on
    /// </summary>
    //public static bool PowerOn = i386.powerOn.Value;

    /// <summary>
    /// The command arguments
    /// </summary>
    public static string[] Args { get; internal set; }

    internal PlayMakerFSM commandFsm;
    internal FsmString saveFile;
    internal Texture2D floppyBlankTexture;
    internal Material floppyBlankMaterial;
    internal GameObject floppyPrefab;

    internal Command currentCommand;
    internal Dictionary<string, Command> commands;

    internal Transform pos;
    internal TextMesh consoleText;
    internal FsmFloat baudFloat;
    internal FsmString exeString;
    internal FsmString commandString;
    internal FsmString oldString;
    internal FsmString textString;
    internal FsmString errorString;
    internal FsmBool playerComputer;
    internal FsmBool modemCord;
    internal FsmBool phonePaid;
    
    /*internal FsmBool fuse;
    internal FsmBool powerOn;*/

    internal I386() {
        commands = new Dictionary<string, Command>();

        gameObject = GameObject.Find("COMPUTER");
        transform = gameObject.transform;

        /*Transform powerButton = transform.Find("Computer/Meshes/386Case/ButtonPower");
        PlayMakerFSM powerButtonFsm = powerButton.GetPlayMaker("Use");
        powerOn = powerButtonFsm.GetVariable<FsmBool>("PowerOn");
        fuse = powerButtonFsm.GetVariable<FsmBool>("Fuse");*/

        pos = transform.Find("SYSTEM/POS");
        Transform commandTransform = transform.Find("SYSTEM/POS/Command");
        commandFsm = commandTransform.GetPlayMaker("Typer");
        FsmState softwareListState = commandFsm.GetState("Software list");
        commandFsm.FsmInject("Software list", onFindCommand, false);

        FsmState customCommandState = commandFsm.AddState("CustomCommand");
        customCommandState.AddTransition("CLOSE", "State 2");
        customCommandState.AddTransition("FINISHED", "Player input");
        commandFsm.FsmInject("CustomCommand", onCustomCommandEnter, false);
        commandFsm.FsmInject("CustomCommand", onCustomCommandUpdate, true);

        softwareListState.AddTransition("CUSTOM", "CustomCommand");

        FsmState driveMemState = commandFsm.GetState("Drive mem");
        driveMemState.AddTransition("CUSTOM", "Player input");
        commandFsm.FsmInject("Drive mem", onCheckCommandExists, false, 1);

        FsmState diskMemState = commandFsm.GetState("Disk mem");
        diskMemState.AddTransition("CUSTOM", "Player input");
        commandFsm.FsmInject("Disk mem", onCheckCommandExists, false, 2);

        exeString = commandFsm.GetVariable<FsmString>("EXE");
        commandString = commandFsm.GetVariable<FsmString>("Command");
        errorString = commandFsm.GetVariable<FsmString>("Error");
        textString = commandFsm.GetVariable<FsmString>("Text");
        oldString = commandFsm.GetVariable<FsmString>("Old");
        baudFloat = commandFsm.GetVariable<FsmFloat>("Baud");
        consoleText = commandTransform.GetComponent<TextMesh>();

        commandFsm.FsmInject("Init", onInit, false, 4);

        saveFile = new FsmString("i386");
        saveFile.Value = "i386.txt";

        playerComputer = PlayMakerGlobals.Instance.Variables.FindFsmBool("PlayerComputer");

        GameObject modemCord_go = GameObject.Find("YARD/Building/LIVINGROOM/Telephone 1/Cord");
        PlayMakerFSM modemCord_fsm = modemCord_go.GetPlayMaker("Use");
        modemCord = modemCord_fsm.GetVariable<FsmBool>("CordModem");

        GameObject phoneBill1_go = GameObject.Find("Systems/PhoneBills1");
        PlayMakerFSM phoneBill1_fsm = phoneBill1_go.GetPlayMaker("Data");
        phonePaid = phoneBill1_fsm.GetVariable<FsmBool>("PhonePaid");

        floppyBlankTexture = new Texture2D(128, 128);
        floppyBlankTexture.LoadImage(Properties.Resources.FLOPPY_BLANK);
        floppyBlankTexture.name = "FLOPPY_IMAGE";

        floppyPrefab = GameObject.Find("diskette(itemx)");
        MeshRenderer renderer = floppyPrefab.transform.Find("mesh").GetComponent<MeshRenderer>();
        Material[] mats = renderer.materials;
        floppyBlankMaterial = new Material(mats[1]);
        floppyBlankMaterial.name = "FLOPPY_IMAGE";
        floppyBlankMaterial.mainTexture = floppyBlankTexture;
    }

    /// <summary>
    /// Get the time it would take to download x bytes at baud speed.
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

    private void exitCommand() {
        commandFsm.SendEvent("FINISHED");
        currentCommand = null;
    }

    // Callbacks/Events

    private void onInit() {
        POS_NewLine();
    }
    private void onCheckCommandExists() {
        if (commandString.Value == string.Empty) {
            commandFsm.SendEvent("CUSTOM");
            return;
        }

        Args = commandString.Value.Split(' ');
        if (Args.Length > 0) {
            exeString.Value = Args[0];
        }
    }
    private void onFindCommand() {
        Args = commandString.Value.Split(' ');
        if (commands.TryGetValue(Args[0], out currentCommand)) {
            commandFsm.SendEvent("CUSTOM");
        }
    }
    private void onCustomCommandUpdate() {
        bool t = true;
        if (currentCommand?.OnUpdate != null) {
            t = currentCommand.OnUpdate.Invoke();
        }

        if (t) {
            exitCommand();
        }
    }
    private void onCustomCommandEnter() {
        bool t = false;
        if (currentCommand?.OnEnter != null) {
            t = currentCommand.OnEnter.Invoke();
        }
        
        if (t) {
            exitCommand();
        }
    }
}
