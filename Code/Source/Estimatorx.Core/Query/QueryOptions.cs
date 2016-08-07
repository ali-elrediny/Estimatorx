﻿using System;
using System.Linq.Expressions;

namespace Estimatorx.Core.Query
{
    public class QueryOptions<TSource, TResult>
    {
        public int Page { get; set; }

        public int PageSize { get; set; }

        public string Sort { get; set; }

        public bool Descending { get; set; }

        public string Filter { get; set; }

        public Expression<Func<TSource, TResult>> Selector { get; set; }
    }
}