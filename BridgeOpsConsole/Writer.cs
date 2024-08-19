using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

class Writer
{
    static void Sym(ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.Write("//");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(" ");
    }

    static public void Affirmative(string s)
    {
        Sym(ConsoleColor.Green);
        Console.WriteLine(s);
    }
    static public void Negative(string s)
    {
        Sym(ConsoleColor.Red);
        Console.WriteLine(s);
    }
    static public void Neutral(string s)
    {
        Sym(ConsoleColor.DarkGray);
        Console.WriteLine(s);
    }
    static public void Message(string s, ConsoleColor col)
    {
        Console.ForegroundColor = col;
        Console.WriteLine(s);
        Console.ForegroundColor = ConsoleColor.White;
    }
    static public void Message(string s)
    {
        Console.WriteLine(s);
    }
    static public void Unerlined(string s)
    {
        int l = s.Length;
        s += "\n";
        for (int n = 0; n < l; ++n)
            s += "-";
        Console.WriteLine(s);
    }
    static public void Header(string s)
    {
        Console.WriteLine("");
        Unerlined(s);
    }

    static public bool YesNo()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("y");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(" / ");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("n");
        Console.ForegroundColor = ConsoleColor.White;

        ConsoleKeyInfo response = Console.ReadKey(true);

        if (response.Key == ConsoleKey.Y)
            return true;
        else
            return false;
    }

    static public void HelpItem(string command, string explanation)
    {
        Console.WriteLine(command);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  " + explanation);
        Console.ForegroundColor = ConsoleColor.White;

        if (command == "[menu name]")
            for (int n = 0; n < ConsoleController.MENU_LAST_INDEX; ++n)
                Console.WriteLine("    " + ConsoleController.MenuName(n));
    }

    static public void Prompt()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("\n" + ConsoleController.CurrentMenuName().ToLower());
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("> ");
    }
}