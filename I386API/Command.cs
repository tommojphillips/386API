using System;
using MSCLoader;
using static I386API.I386;

namespace I386API;

/// <summary>
/// i386 PC POS Command
/// </summary>
public class Command {
    /// <summary>
    /// Called on command enter (start)
    /// Return true to exit commmand.
    /// Return false to keep running.
    /// </summary>
    public Func<bool> OnEnter;
    /// <summary>
    /// Called every frame when command is running. 
    /// Return true to exit commmand.
    /// Return false to keep running.
    /// </summary>
    public Func<bool> OnUpdate;

    /// <summary>
    /// INTERNAL
    /// </summary>
    internal Command() {
        OnEnter = null;
        OnUpdate = null;
    }

    /// <summary>
    /// Create an external command
    /// </summary>
    /// <param name="exe">EXE name</param>
    /// <param name="enter">Enter func</param>
    /// <param name="update">Update func</param>
    public static Command Create(string exe, Func<bool> enter, Func<bool> update) {
        
        if (i386 == null) {
            ModConsole.Error($"[386API] Not ready. Your mod is loading before 386API is initialized. Are you using PreLoad?");
            return null;
        }

        // check commands. we shouldnt have this command in both lists.
        if (i386.builtInCommands.ContainsKey(exe)) {
            ModConsole.Error($"[386API] {exe} already exists in builtin-commands. You cannot have an external-command that is also a builtin-command");
            return null;
        }

        if (i386.commands.ContainsKey(exe)) {
            ModConsole.Error($"[386API] {exe} already exists in external-commands");
            return null;
        }

        Command command = new Command();
        command.OnEnter = enter;
        command.OnUpdate = update;
        i386.commands.Add(exe, command);
        return command;
    }

    /// <summary>
    /// Create a built in command
    /// </summary>
    /// <param name="exe">EXE name</param>
    /// <param name="enter">Enter func</param>
    /// <param name="update">Update func</param>
    public static Command CreateBuiltIn(string exe, Func<bool> enter, Func<bool> update) {

        if (i386 == null) {
            ModConsole.Error($"[386API] Not ready. Your mod is loading before 386API is initialized. Are you using PreLoad?");
            return null;
        }

        for (int i = 0; i < i386.bbsCommands.Count; i++) {
            if (i386.bbsCommands[i].exe == exe) {
                ModConsole.Error($"[386API] {exe} already exists in bbs-commands. You cannot have a builtin-command that is also a bbs-command.");
                return null;
            }
        }
        
        if (i386.commands.ContainsKey(exe)) {
            ModConsole.Error($"[386API] {exe} already exists in external-commands. You cannot have a builtin-command that is also an external-command.");
            return null;
        }

        if (i386.builtInCommands.ContainsKey(exe)) {
            ModConsole.Error($"[386API] {exe} already exists in built in commands");
            return null;
        }

        Command command = new Command();
        command.OnEnter = enter;
        command.OnUpdate = update;
        i386.builtInCommands.Add(exe, command);
        return command;
    }
}
