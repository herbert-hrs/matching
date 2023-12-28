using System;
using System.Text;
using System.Globalization;

namespace MatchingTest.Initiator
{
    public class Utils
    {
        static private readonly Random _random = new Random();

        static public string GenerateID()
        {   
            var builder = new StringBuilder(15);
            char offset = 'A';
            const int lettersOffset = 26;

            for (var i = 0; i < 15; i++)
            {
                var @char = (char)_random.Next(offset, offset + lettersOffset);
                builder.Append(@char);
            }

            return builder.ToString();
        }
    }
}
