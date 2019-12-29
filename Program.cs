/* 
 * Amiga Bytekiller.NET (w) by Alexander Dimitriadis
 * Based on a portable C-source by Frank Wille
 * Original Motorola 68000 code by Lord Blitter '87
 * 
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teyko.Amiga.Cruncher
{
  class Program
  {
    static int Main(string[] args)
    {
      uint scanWidth;
      Console.WriteLine("Amiga Bytekiller.NET (w) Alexander Dimitriadis 2019");
      try
      {
        if (args.Length == 3
            && args[0].ToLowerInvariant() == "decrunch")
        {
          Bytekiller.DeCrunch(args[1], args[2]);
          Console.WriteLine($"{args[1]} ({new FileInfo(args[1]).Length}) -> {args[2]} ({new FileInfo(args[2]).Length})");
        }
        else if (args.Length == 4
                 && args[0].ToLowerInvariant() == "crunch"
                 && UInt32.TryParse(args[1], out scanWidth)
                 && scanWidth >= 8
                 && scanWidth <= 4096)
        {
          Bytekiller.Crunch(args[2], args[3], scanWidth);
          Console.WriteLine($"{args[2]} ({new FileInfo(args[2]).Length}) -> {args[3]} ({new FileInfo(args[3]).Length})");
        }
        else
        {
          Console.WriteLine("Usage   : bytekiller.exe [action] <scan-width> infile outfile");
          Console.WriteLine("          [action] = crunch, decrunch");
          Console.WriteLine("          <scan-width> = 8..4096 (only needed for action 'crunch')");
          Console.WriteLine("Example : bytekiller.exe crunch 4096 infile outfile");
          Console.WriteLine("          bytekiller.exe decrunch infile outfile");
          return 1;
        }
        Console.WriteLine("Done");
        return 0;
      }
      catch(Exception x)
      {
        Console.WriteLine($"Error: {x.Message}");
        return 1;
      }
    }
  }
}
