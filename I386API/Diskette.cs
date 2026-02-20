using UnityEngine;
using HutongGames.PlayMaker;
using MSCLoader;
using HutongGames.PlayMaker.Actions;
using System.Collections.Generic;

using static I386API.I386;

namespace I386API;

/// <summary>
/// 3.5" Floppy Diskette for i386 PC
/// </summary>
public class Diskette {
    internal FsmString _exe;
    internal FsmFloat _kb;

    internal FsmString _tag_exe;
    internal FsmString _tag_in_drive;
    internal FsmString _tag_kb;
    internal FsmString _tag_pos;
    
    internal GameObject _gameObject;
    internal MeshRenderer _renderer;
    internal MeshFilter _filter;

    /// <summary>
    /// diskette game object
    /// </summary>
    public GameObject gameObject {
        get => _gameObject;
        internal set => _gameObject = value;
    }

    /// <summary>
    /// Diskette exe name
    /// </summary>
    public string exe {
        get => _exe.Value; 
        set => _exe.Value = value;
    }

    /// <summary>
    /// Diskette size in kb
    /// </summary>
    public float kb {
        get => _kb.Value; 
        set => _kb.Value = value;
    }

    /// <summary>
    /// [INTERNAL]
    /// </summary>
    internal Diskette() {
        _exe = null;
        _kb = null;
        _tag_exe = null;
        _tag_in_drive = null;
        _tag_kb = null;
        _tag_pos = null;
        _gameObject = null;
        _renderer = null;
        _filter = null;
    }

    /// <summary>
    /// Set the diskette label texture
    /// </summary>
    /// <param name="texture">The texture</param>
    public void SetTexture(Texture2D texture) {
        if (_renderer == null) {
            return;
        }

        _renderer.materials[1].mainTexture = texture;
    }

    /// <summary>
    /// Create new Diskette
    /// </summary>
    /// <param name="exe">The exe name</param>
    /// <param name="defaultPosition">Default position if save info doesnt exist</param>
    /// <param name="defaultEulerAngles">Default rotation if save info doesnt exist</param>
    public static Diskette Create(string exe, Vector3 defaultPosition = default, Vector3 defaultEulerAngles = default) {
        Diskette diskette = new Diskette();

        GameObject g = GameObject.Instantiate(i386.floppyPrefab);
        g.name = "diskette(itemx)";
        g.transform.position = defaultPosition;
        g.transform.eulerAngles = defaultEulerAngles;
        PlayMakerFSM fsm = g.GetPlayMaker("Use");
        if (fsm == null) {
            return null;
        }

        diskette._kb = fsm.GetVariable<FsmFloat>("KB");
        diskette._exe = fsm.GetVariable<FsmString>("EXE");
        diskette._tag_exe = fsm.GetVariable<FsmString>("UniqueTagEXE");
        diskette._tag_in_drive = fsm.GetVariable<FsmString>("UniqueTagInDrive");
        diskette._tag_kb = fsm.GetVariable<FsmString>("UniqueTagKB");
        diskette._tag_pos = fsm.GetVariable<FsmString>("UniqueTagPos");

        diskette.gameObject = g;

        FsmState s1 = fsm.GetState("Load");
        (s1.Actions[0] as LoadTransform).saveFile = i386.saveFile;
        (s1.Actions[1] as LoadBool).saveFile = i386.saveFile;
        (s1.Actions[2] as LoadString).saveFile = i386.saveFile;
        (s1.Actions[3] as LoadFloat).saveFile = i386.saveFile;

        FsmState s2 = fsm.GetState("Save");
        (s2.Actions[0] as SaveTransform).saveFile = i386.saveFile;
        (s2.Actions[1] as SaveBool).saveFile = i386.saveFile;
        (s2.Actions[2] as SaveString).saveFile = i386.saveFile;
        (s2.Actions[3] as SaveFloat).saveFile = i386.saveFile;

        FsmState s3 = fsm.GetState("State 4");
        (s3.Actions[1] as Exists).saveFile = i386.saveFile;

        Transform mesh = diskette.gameObject.transform.Find("mesh");
        if (mesh != null) {
            diskette._renderer = mesh.GetComponent<MeshRenderer>();
            diskette._filter = mesh.GetComponent<MeshFilter>();
            if (diskette._renderer != null && diskette._filter != null) {

                Mesh original = diskette._filter.sharedMesh;
                Mesh m = GameObject.Instantiate(original);
                diskette._filter.mesh = m;

                int[] triangles = m.GetTriangles(1);
                Vector2[] uvs = m.uv;

                Rect atlasRect = new Rect(0.3125f, 0.125f, 0.0625f, 0.0625f);

                HashSet<int> usedVerts = new HashSet<int>(triangles);

                foreach (int vertIndex in usedVerts) {
                    Vector2 uv = uvs[vertIndex];

                    uv.x = (uv.x - atlasRect.x) / atlasRect.width;
                    uv.y = (uv.y - atlasRect.y) / atlasRect.height;

                    uvs[vertIndex] = uv;
                }

                m.uv = uvs;

                Material[] mats = diskette._renderer.materials;
                mats[1] = i386.floppyBlankMaterial;
                diskette._renderer.materials = mats;
            }
        }
        
        diskette.exe = exe;
        diskette._tag_exe.Value = $"floppy_{exe}_exe";
        diskette._tag_in_drive.Value = $"floppy_{exe}_in_drive";
        diskette._tag_kb.Value = $"floppy_{exe}_kb";
        diskette._tag_pos.Value = $"floppy_{exe}_pos";

        diskette.kb = 320;

        return diskette;
    }
}
