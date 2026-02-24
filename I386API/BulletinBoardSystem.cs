using MSCLoader;

using static I386API.I386;

namespace I386API;

internal class BBS_Command {
    internal string exe;
    internal int size;
}

/// <summary>
/// TELE BBS
/// </summary>
public class BulletinBoardSystem {

    /// <summary>
    /// INTERNAL
    /// </summary>
    internal BulletinBoardSystem() { }

    /// <summary>
    /// Add command to BBS
    /// </summary>
    /// <param name="exe">The EXE name</param>
    /// <param name="kb">The EXE size</param>
    public static void Add(string exe, int kb = 320) {

        if (i386 == null) {
            ModConsole.Error($"[386API] Not ready. Your mod is loading before 386API is initialized. Are you using PreLoad?");
            return;
        }

        if (i386.builtInCommands.ContainsKey(exe)) {
            ModConsole.Error($"[386API] {exe} already exists in builtin-commands. You cannot have a builtin-command that is also a bbs-command.");
            return;
        }

        for (int i = 0; i < i386.bbsCommands.Count; i++) {
            if (i386.bbsCommands[i].exe == exe) {
                ModConsole.Error($"[386API] {exe} already exists in bbs-commands.");
                return;
            }
        }

        if (!i386.commands.ContainsKey(exe)) {
            ModConsole.Error($"[386API] {exe} not found in external-commands. bbs-commands require an external-command.");
            return;
        }

        BBS_Command command = new BBS_Command();
        command.exe = exe;
        command.size = kb;
        i386.bbsCommands.Add(command);
    }
}
