using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.Utilities
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<IEnumerable<T>> Partition<T>(this IEnumerable<T> values, int chunkSize)
        {
            while (values.Any())
            {
                yield return values.Take(chunkSize).ToList();
                values = values.Skip(chunkSize).ToList();
            }
        }

    }
}
