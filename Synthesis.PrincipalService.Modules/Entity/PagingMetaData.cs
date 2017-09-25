using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synthesis.PrincipalService.Entity
{
    public class PagingMetadata<T>
    {
        public string ContinuationToken { get; set; }

        /// <summary>
        /// The number of records that macth the searched criteria
        /// </summary>
        public int CurrentCount { get; set; }

        /// <summary>
        /// States whether the number of records that are in last page.
        /// </summary>
        public bool IsLastChunk { get; set; }

        /// <summary>
        /// The search criteria entered by the user in the search field
        /// </summary>
        public string SearchValue { get; set; }

        /// <summary>
        /// The name of the column to sort
        /// </summary>
        public string SortColumn { get; set; }

        /// <summary>
        /// Flag to indicate data is sorted in decending order.
        /// </summary>
        public bool SortDescending { get; set; }

        public List<T> List { get; set; }

    }

}
