using System;

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
    /// Create a i386 POS commmand
    /// </summary>
    /// <param name="exe">The exe name</param>
    /// <param name="enter">Enter func</param>
    /// <param name="update">Update func</param>
    public static Command Create(string exe, Func<bool> enter, Func<bool> update) {
        Command command = new Command();
        command.OnEnter = enter;
        command.OnUpdate = update;

        if (i386.commands.ContainsKey(exe)) {
            i386.commands[exe] = command;
        }
        else {
            i386.commands.Add(exe, command);
        }
        return command;
    }
}
