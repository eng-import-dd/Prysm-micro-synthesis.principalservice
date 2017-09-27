using System.Collections.Generic;

namespace Synthesis.PrincipalService.Entity
{
    public class PagingMetadata<T>
    {
        /// <summary>
        /// The number of records that macth the searched criteria
        /// </summary>
        public int CurrentCount { get; set; }
        
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

        /// <summary>
        /// The continuation token get next chunk of data
        /// </summary>
        public string ContinuationToken { get; set; }

        /// <summary>
        /// Flag to indicate the last chunk of data
        /// </summary>
        public bool IsLastChunk { get; set; }
        /// <summary>
        /// List of objects to be returned
        /// </summary>
        public List<T> List { get; set; }
    }

}
