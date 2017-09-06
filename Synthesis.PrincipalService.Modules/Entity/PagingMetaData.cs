using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synthesis.PrincipalService.Entity
{
    public class PagingMetaData<T>
    {
        /// <summary>
        /// The total count of all records the current user can access
        /// </summary>
        public Int32 TotalCount { get; set; }

        /// <summary>
        /// The number of records that macth the searched criteria
        /// </summary>
        public Int32 CurrentCount { get; set; }

        /// <summary>
        /// The page that is requested. The first page is 1.
        /// </summary>
        public Int32 CurrentPage { get; set; }

        /// <summary>
        /// The search criteria entered by the user in the search field
        /// </summary>
        public string SearchFilter { get; set; }

        /// <summary>
        /// The name of the column to sort
        /// </summary>
        public string SortColumn { get; set; }

        /// <summary>
        /// The sort order
        /// </summary>
        public DataSortOrder SortOrder { get; set; }

        public List<T> List { get; set; }

    }

    public enum DataSortOrder { Ascending, Descending }
}
