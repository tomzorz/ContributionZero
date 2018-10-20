using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace OtpMatrix
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            byte[][] GetMatrix2(int size, double p)
            {
                var rnd = new Random();

                var matrix = new byte[size+2][];
                matrix[0] = new byte[size];
                for (var i = 0; i < size; i++)
                {
                    matrix[0][i] = 3;
                }
                for (var i = 1; i < size+1; i++)
                {
                    matrix[i] = new byte[size];
                    for (var j = 0; j < size; j++)
                    {
                        matrix[i][j] = rnd.NextDouble() < p ? (byte)3 : (byte)0;
                    }
                }
                matrix[size+1] = new byte[size];
                for (var i = 0; i < size; i++)
                {
                    matrix[size+1][i] = 3;
                }
                return matrix;
            }

            Console.Write("Enter size: ");
            var n = int.Parse(Console.ReadLine());
            Console.Write("Enter threads: ");
            var threads = int.Parse(Console.ReadLine());
            Console.Write("Enter tries per round: ");
            var tries = int.Parse(Console.ReadLine());

            while (true)
            {
                Console.Write("Enter p: ");
                var d = double.Parse(Console.ReadLine(), CultureInfo.InvariantCulture);
                var sw = new Stopwatch();
                sw.Start();
                var succ = 0;
                Parallel.For(0, tries, new ParallelOptions
                {
                    MaxDegreeOfParallelism = threads
                }, i =>
                {
                    Console.Write($"Trying {i} | ");
                    var csw = new Stopwatch();
                    csw.Start();
                    var m = GetMatrix2(n, d);
                    m = Fill(m, (0, 0), 4, n);
                    var hs = m.Last().Any(x => x == 4);
                    csw.Stop();
                    succ += hs ? 1 : 0;
                    Console.Write(hs ? $"Try {i} OK - {csw.Elapsed.TotalSeconds} s | " : $"Try {i} FAIL - {csw.Elapsed.TotalSeconds} s | ");
                });
                sw.Stop();
                var res = succ / (double)tries;
                Console.WriteLine($"{succ} of {tries} - {res} - took {sw.Elapsed.TotalSeconds} s");
            }
        }

        private static void PrintMatrix(byte[][] m)
        {
            foreach (var i in m)
            {
                foreach (var i1 in i)
                {
                    Console.Write(i1);
                }
                Console.WriteLine();
            }
        }

        private static byte[][] Fill(byte[][] bmp, (int X, int Y) pt, byte replacementColor, int size)
        {
            var pixels = new Stack<(int X, int Y)>();
            var targetColor = bmp[pt.X][pt.Y];
            pixels.Push(pt);

            while (pixels.Count > 0)
            {
                var a = pixels.Pop();
                if (a.X >= size+2 || a.X < 0 || a.Y >= size || a.Y < 0) continue;
                if (bmp[a.X][a.Y] != targetColor) continue;
                bmp[a.X][a.Y] = replacementColor;
                pixels.Push((a.X - 1, a.Y));
                pixels.Push((a.X + 1, a.Y));
                pixels.Push((a.X, a.Y - 1));
                pixels.Push((a.X, a.Y + 1));
            }

            return bmp;
        }
    }
}