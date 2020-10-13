//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Cli
{
    using System;
    using System.Globalization;
    using Abune.Server.Cli.Commands;

    /// <summary>Cli runtime program.</summary>
    public static class Program
    {
        /// <summary>Defines the entry point of the application.</summary>
        /// <param name="args">The arguments.</param>
        /// <exception cref="ArgumentOutOfRangeException">args</exception>
        public static void Main(string[] args)
        {
            try
            {
                if (args == null || args.Length < 3)
                {
                    throw new ArgumentOutOfRangeException(nameof(args));
                }
                
                string command = args[0];
                string[] commandParams = SubArray(args, 1, args.Length - 1);
                if (string.Compare(command, "loadtest", true, CultureInfo.InvariantCulture) == 0)
                {
                    var test = new LoadTest(Console.Out, commandParams);
                    test.Run();
                }

                Console.ReadLine();
            }
#pragma warning disable CA1031 // Keine allgemeinen Ausnahmetypen abfangen
            catch (Exception e)
#pragma warning restore CA1031 // Keine allgemeinen Ausnahmetypen abfangen
            {
                Console.Error.WriteLine($"{e.GetType()}: {e.Message}");
                Console.Error.WriteLine($"StackTrage: {e.StackTrace}");
            }
        }

        private static T[] SubArray<T>(T[] data, int index, int length)
        {
            T[] result = new T[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }
    }

}
