﻿
namespace Synthesis.PrincipalService.Entity
{
    public class ServerSidePagingParams
    {
        public string SearchValue { get; set; }

        public string ContinuationToken { get; set; }

        public string SortColumn { get; set; }
        public DataSortOrder SortOrder { get; set; }
    }
}