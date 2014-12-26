﻿using System;
using Estimatorx.Core;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace Estimatorx.Data.Mongo.Mapping
{
    public class EstimateMap : BsonClassMap<Estimate>
    {
        public EstimateMap()
        {
            AutoMap();

            GetMemberMap(p => p.FactorId)
                .SetRepresentation(BsonType.String);
        }
    }
}