using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Combinators
{
    public static class Combinators
    {
        public static List<List<T>> UniqueCombinations<T>(List<T> inputList, int minimumItems = 1, int maximumItems = int.MaxValue)
        {
            int nonEmptyCombinations = (int)Math.Pow(2, inputList.Count) - 1;
            List<List<T>> listOfLists = new List<List<T>>(nonEmptyCombinations + 1);

            // Optimize generation of empty combination, if empty combination is wanted
            if (minimumItems == 0) listOfLists.Add(new List<T>());

            if (minimumItems <= 1 && maximumItems >= inputList.Count)
            {
                // Simple case, generate all possible non-empty combinations
                for (int bitPattern = 1; bitPattern <= nonEmptyCombinations; bitPattern++)
                    listOfLists.Add(GenerateCombination(inputList, bitPattern));
            }
            else
            {
                // Not-so-simple case, avoid generating the unwanted combinations
                for (int bitPattern = 1; bitPattern <= nonEmptyCombinations; bitPattern++)
                {
                    int bitCount = CountBits(bitPattern);
                    if (bitCount >= minimumItems && bitCount <= maximumItems)
                        listOfLists.Add(GenerateCombination(inputList, bitPattern));
                }
            }
            return listOfLists;
        }

        /// <summary>
        /// Sub-method of ItemCombinations() method to generate a combination based on a bit pattern.
        /// </summary>
        private static List<T> GenerateCombination<T>(List<T> inputList, int bitPattern)
        {
            List<T> thisCombination = new List<T>(inputList.Count);
            for (int j = 0; j < inputList.Count; j++)
            {
                if ((bitPattern >> j & 1) == 1)
                    thisCombination.Add(inputList[j]);
            }
            return thisCombination;
        }

        /// <summary>
        /// Sub-method of ItemCombinations() method to count the bits in a bit pattern. Based on this:
        /// https://graphics.stanford.edu/~seander/bithacks.html#CountBitsSetKernighan
        /// </summary>
        private static int CountBits(int bitPattern)
        {
            int numberBits = 0;
            while (bitPattern != 0)
            {
                numberBits++;
                bitPattern &= bitPattern - 1;
            }
            return numberBits;
        }


        public static IEnumerable<IEnumerable<T>> CartesianProduct<T>(this IEnumerable<IEnumerable<T>> sequences)
        {
            IEnumerable<IEnumerable<T>> result = new List<List<T>>() { Enumerable.Empty<T>().ToList() }; // { Enumerable.Empty<T>() };
            //IEnumerable<IEnumerable<T>> result = new []{ Enumerable.Empty<T>() };
            foreach (var sequence in sequences)
            {
                var localSequence = sequence;
                result = result.SelectMany(
                  _ => localSequence,
                  (seq, item) => seq.Concat(new[] { item }).ToList()
                ).ToList();
            }
            return result;
        }

    }
}






