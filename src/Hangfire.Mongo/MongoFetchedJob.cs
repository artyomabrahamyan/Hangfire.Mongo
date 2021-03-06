﻿using System;
using System.Threading;
using Hangfire.Logging;
using Hangfire.Mongo.Database;
using Hangfire.Mongo.Dto;
using Hangfire.Storage;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Hangfire.Mongo
{
    /// <summary>
    /// Hangfire fetched job for Mongo database
    /// </summary>
    public class MongoFetchedJob : IFetchedJob
    {
        private static readonly ILog Logger = LogProvider.For<MongoFetchedJob>();
        private readonly HangfireDbContext _connection;
        private readonly ObjectId _id;

        private bool _disposed;

        private bool _removedFromQueue;

        private bool _requeued;

        /// <summary>
        /// Constructs fetched job by database connection, identifier, job ID and queue
        /// </summary>
        /// <param name="connection">Database connection</param>
        /// <param name="id">Identifier</param>
        /// <param name="jobId">Job ID</param>
        /// <param name="queue">Queue name</param>
        public MongoFetchedJob(HangfireDbContext connection, ObjectId id, ObjectId jobId, string queue)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _id = id;
            JobId = jobId.ToString();
            Queue = queue ?? throw new ArgumentNullException(nameof(queue));
        }

        /// <summary>
        /// Job ID
        /// </summary>
        public string JobId { get; }

        /// <summary>
        /// Queue name
        /// </summary>
        public string Queue { get; }

        /// <summary>
        /// Removes fetched job from a queue
        /// </summary>
        public virtual void RemoveFromQueue()
        {
            _connection
               .JobGraph.OfType<JobQueueDto>()
               .DeleteOne(Builders<JobQueueDto>.Filter.Eq(_ => _.Id, _id));
            if (Logger.IsTraceEnabled())
            {
                Logger.Trace($"Remove job '{JobId}' from queue '{Queue}'");
            }
            _removedFromQueue = true;
        }

        /// <summary>
        /// Puts fetched job into a queue
        /// </summary>
        public virtual void Requeue()
        {
            _connection.JobGraph.OfType<JobQueueDto>().FindOneAndUpdate(
                Builders<JobQueueDto>.Filter.Eq(_ => _.Id, _id),
                Builders<JobQueueDto>.Update.Set(_ => _.FetchedAt, null));
            _connection.Notifications.InsertOne(NotificationDto.JobEnqueued(Queue), new InsertOneOptions
            {
                BypassDocumentValidation = false
            });
            if (Logger.IsTraceEnabled())
            {
                Logger.Trace($"Requeue job '{JobId}' from queue '{Queue}'");
            }
            _requeued = true;
        }

        /// <summary>
        /// Disposes the object
        /// </summary>
        public virtual void Dispose()
        {
            if (_disposed) return;

            if (!_removedFromQueue && !_requeued)
            {
                Requeue();
            }

            _disposed = true;
        }
    }
}