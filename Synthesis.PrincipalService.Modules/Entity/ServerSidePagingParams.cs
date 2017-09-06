﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Synthesis.PrincipalService.Enums;

namespace Synthesis.PrincipalService.Entity
{
    public class ServerSidePagingParams
    {
        public string SearchValue { get; set; }
        public Int32 PageNumber { get; set; }
        public Int32 PageSize { get; set; }
        public string SortColumn { get; set; }
        public DataSortOrder SortOrder { get; set; }
    }
}
