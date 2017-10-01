using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using AsciiUml;
using AsciiUml.Commands;
using AsciiUml.Geo;
using AsciiUml.UI;
using AsciiUml.UI.GuiLib;
using LanguageExt;
using static AsciiUml.Extensions;

static internal class KeyHandler
{
    private static List<ICommand> Noop => new List<ICommand>();

    public static List<ICommand> HandleKeyPress(State state, ConsoleKeyInfo key, List<List<ICommand>> commandLog, UmlWindow umlWindow)
    {
        return ControlKeys(state, key, commandLog)
            .IfNone(() => ShiftKeys(state, key)
            .IfNone(() => HandleKeys(state, key, commandLog, umlWindow)
            .IfNone(() => Noop)));
    }

    private static Option<List<ICommand>> HandleKeys(State state, ConsoleKeyInfo key, List<List<ICommand>> commandLog, UmlWindow umlWindow)
    {
        var model = state.Model;
        var selected = state.SelectedIndexInModel;
        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                if (state.TheCurser.Pos.Y > 0)
                    return MoveCursorAndSelectPaintable(Vector.DeltaNorth);
                break;
            case ConsoleKey.DownArrow:
                if (state.TheCurser.Pos.Y < State.MaxY - 2)
                    return MoveCursorAndSelectPaintable(Vector.DeltaSouth);
                break;
            case ConsoleKey.LeftArrow:
                if (state.TheCurser.Pos.X > 0)
                    return MoveCursorAndSelectPaintable(Vector.DeltaWest);
                break;
            case ConsoleKey.RightArrow:
                if (state.TheCurser.Pos.X < State.MaxX - 2)
                    return MoveCursorAndSelectPaintable(Vector.DeltaEast);
                break;

            case ConsoleKey.Spacebar:
                return SelectDeselect(selected, state);

            case ConsoleKey.S:
                return PrintIdsAndLetUserSelectObject(state)
                    .Match(x => Lst(new SelectObject(x, true)), () => Noop);

            case ConsoleKey.X:
            case ConsoleKey.Delete:
                return Lst(new DeleteSelectedElement());

            case ConsoleKey.D:
                return Lst(new CreateDatabase(state.TheCurser.Pos));

            case ConsoleKey.H:
                return HelpScreen(umlWindow);

            case ConsoleKey.B:
                return ConsoleInputColors(() =>
                    CommandParser.TryReadLineWithCancel("Create box. Title: ")
                    .Match(x => Lst(new CreateBox(state.TheCurser.Pos, x)), () => Noop));

            case ConsoleKey.C:
                return ConnectObjects(state);
            case ConsoleKey.L:
                return SlopedLine(state);

            case ConsoleKey.T:
                return ConsoleInputColors(() =>
                    CommandParser.TryReadLineWithCancel("Create a label. Text: ")
                    .Match(x => Lst(new CreateLabel(state.TheCurser.Pos, x)), () => Noop));

            case ConsoleKey.R:
                return Rotate(selected, model);

            case ConsoleKey.Enter:
                return CommandMode(state, commandLog);
        }
        return Noop;
    }

    private static List<ICommand> SlopedLine(State state)
    {
        return Lst(new CreateSlopedLine(state.TheCurser.Pos));
    }

    private static List<ICommand> SelectDeselect(int? selected, State state)
    {
        if (selected.HasValue)
            return Lst(new ClearSelection());

        var obj = GetIdOfCursorPosOrAskUserForId(state)
            .Match(x => Lst(new SelectObject(x.Item1, x.Item2)),
                () => Lst(new ClearSelection()));
        return obj;
    }

    private static List<ICommand> HelpScreen(UmlWindow umlWindow)
    {
        var titled = new TitledWindow(umlWindow, "About...")
        {
            BackGround = ConsoleColor.DarkBlue,
            Foreground = ConsoleColor.White
        };
        var pop = new Popup(titled, @"
*  *  ****  *     ****    
*  *  *     *     *  *    
****  **    *     ****    
*  *  *     *     *       
*  *  ****  ****  *       
space ................ (un)select object at cursor or choose object
s .................... select an object
cursor keys........... move cursor or selected object
shift + cursor ....... move object under cursor
r .................... rotate selected object (only text label)
ctrl + cursor ........ move selected object (only box)
b .................... Create a Box
c .................... Create a connection between boxes
d .................... Create a Database
t .................... Create a text label
l .................... Create a free style line
x / Del............... Delete selected object
Esc .................. Abort input
ctrl+c ............... Exit program");
        return Noop;
    }

    private static List<ICommand> ConnectObjects(State state)
    {
        Console.WriteLine("Connect from object: ");

        var cmds = Noop;
        PrintIdsAndLetUserSelectObject(state)
            .IfSome(from =>
            {
                Console.WriteLine("");
                Console.WriteLine("to object: ");
                PrintIdsAndLetUserSelectObject(state)
                    .IfSome(to => { cmds.Add(new CreateLine(@from, to, LineKind.Connected)); });
            });
        return cmds;
    }

    private static List<ICommand> CommandMode(State state, List<List<ICommand>> commandLog)
    {
        var cmd = CommandParser.TryReadLineWithCancel("Enter command: ");

        return cmd.Match(x => {
            switch (x)
            {
                case "dump":
                    Program.PrintCommandLog(commandLog);
                    break;
                case "database":
                    return Lst(new CreateDatabase(state.TheCurser.Pos));
            }
            return Noop;
        }, () => Noop);
    }

    private static List<ICommand> Rotate(int? selected, List<IPaintable<object>> model)
    {
        return selected.Match(x =>
            {
                if (model[x] is Label)
                    return Lst(new RotateSelectedElement(x));
                Screen.PrintErrorAndWaitKey("Only labels can be rotated");
                return Noop;
            },
            () =>
            {
                Screen.PrintErrorAndWaitKey("Nothing is selected");
                return Noop;
            });
    }


    private static Option<List<ICommand>> ControlKeys(State state, ConsoleKeyInfo key, List<List<ICommand>> commandLog)
    {
        if ((key.Modifiers & ConsoleModifiers.Control) == 0)
            return Option<List<ICommand>>.None;

        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
            case ConsoleKey.DownArrow:
            case ConsoleKey.LeftArrow:
            case ConsoleKey.RightArrow:
                return ControlCursor(state, key);

            case ConsoleKey.S:
                Program.PrintCommandLog(commandLog);
                return Noop;

            default:
                return Option<List<ICommand>>.None;
        }
    }

    private static Option<List<ICommand>> ControlCursor(State state, ConsoleKeyInfo key)
    {
        return SelectTemporarily(state, x =>
        {
            return x.GetSelected().Bind(el=>
            {
                if (el is Box)
                {
                    switch (key.Key)
                    {
                        case ConsoleKey.UpArrow:
                            return Lst(new ResizeSelectedBox(Vector.DeltaNorth));
                        case ConsoleKey.DownArrow:
                            return Lst(new ResizeSelectedBox(Vector.DeltaSouth));
                        case ConsoleKey.LeftArrow:
                            return Lst(new ResizeSelectedBox(Vector.DeltaWest));
                        case ConsoleKey.RightArrow:
                            return Lst(new ResizeSelectedBox(Vector.DeltaEast));
                    }
                }
                if (el is SlopedLine2)
                {
                    switch (key.Key)
                    {
                        case ConsoleKey.UpArrow:
                            return Lst(new ResizeSelectedBox(Vector.DeltaNorth));
                        case ConsoleKey.DownArrow:
                            return Lst(new ResizeSelectedBox(Vector.DeltaSouth));
                        case ConsoleKey.LeftArrow:
                            return Lst(new ResizeSelectedBox(Vector.DeltaWest));
                        case ConsoleKey.RightArrow:
                            return Lst(new ResizeSelectedBox(Vector.DeltaEast));
                    }
                }
                return Noop;

            }).ToList();
            
        });
    }

    private static Option<List<ICommand>> ShiftKeys(State state, ConsoleKeyInfo key)
    {
        if ((key.Modifiers & ConsoleModifiers.Shift) == 0)
            return Option<List<ICommand>>.None;

        var commands = SelectTemporarily(state, x => {
            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    return MoveCursorAndSelectPaintable(Vector.DeltaNorth);
                case ConsoleKey.DownArrow:
                    return MoveCursorAndSelectPaintable(Vector.DeltaSouth);
                case ConsoleKey.LeftArrow:
                    return MoveCursorAndSelectPaintable(Vector.DeltaWest);
                case ConsoleKey.RightArrow:
                    return MoveCursorAndSelectPaintable(Vector.DeltaEast);
            }
            return Noop;
        });
        return commands;
    }

    private static List<ICommand> MoveCursorAndSelectPaintable(Coord direction)
    {
        return Lst(new MoveCursor(direction), new MoveSelectedPaintable(direction));
    }

    private static Option<Tuple<int, bool>> GetIdOfCursorPosOrAskUserForId(State state)
    {
        return state.Canvas.GetOccupants(state.TheCurser.Pos).ToOption()
            .Match(x => Tuple.Create(x, false),
                () => PrintIdsAndLetUserSelectObject(state).Select(x => Tuple.Create(x, true)));
    }

    private static Option<int> PrintIdsAndLetUserSelectObject(State state)
    {
        var cursorTop = Console.CursorTop;
        Screen.SetConsoleGetInputColors();
        PrintIdsOfModel(state.Model);
        Console.SetCursorPosition(0, cursorTop);

        var res = GetISelectableElement(state.Model);

        Screen.SetConsoleStandardColor();

        return res;
    }

    private static T ConsoleInputColors<T>(Func<T> code)
    {
        Screen.SetConsoleGetInputColors();
        var res = code();
        Screen.SetConsoleStandardColor();
        return res;
    }

    private static Option<int> GetISelectableElement(List<IPaintable<object>> model)
    {
        return CommandParser.ReadInt(model.Select(x=>x.Id).ToArray(),  "Select object: ")
            .Bind(x => {
                if (model.SingleOrDefault(b => b.Id == x) is ISelectable)
                    return x;
                Screen.PrintErrorAndWaitKey("Not a selectable object");
                return GetISelectableElement(model);
            });
    }

    private static void PrintIdsOfModel(List<IPaintable<object>> model)
    {
        foreach (var selectable in model.OfType<ISelectable>())
        {
            Console.SetCursorPosition(selectable.Pos.X, selectable.Pos.Y + 1);
            Console.Write(selectable.Id);
        }
    }

    public static Option<List<ICommand>> SelectTemporarily(State state, Func<State, List<ICommand>> code)
    {
        return state.SelectedId.Match(
            _ => code(state),
            () => state.Canvas.GetOccupants(state.TheCurser.Pos).Match(
                x =>
                {
                    var commands = code(state);
                    return commands.Count==0?Option<List<ICommand>>.None: 
                    Lst(new SelectObject(x, false)).Append(commands).Append(Lst(new ClearSelection()))
                        .ToList();
                },
                () => Noop));
    }
}