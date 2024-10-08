﻿using PowerStore.Core.Events;
using PowerStore.Domain.Customers;
using PowerStore.Domain.Data;
using MediatR;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PowerStore.Services.Events
{
    public class CustomerDeletedEventHandler : INotificationHandler<EntityDeleted<Customer>>
    {
        private readonly IRepository<ExternalAuthenticationRecord> _externalAuthenticationRepository;
        public CustomerDeletedEventHandler(IRepository<ExternalAuthenticationRecord> externalAuthenticationRepository)
        {
            _externalAuthenticationRepository = externalAuthenticationRepository;
        }
        public async Task Handle(EntityDeleted<Customer> notification, CancellationToken cancellationToken)
        {
            var externalrecords = _externalAuthenticationRepository.Table.Where(x => x.CustomerId == notification.Entity.Id).ToList();
            foreach (var item in externalrecords)
            {
                await _externalAuthenticationRepository.DeleteAsync(item);
            }
        }
    }
}
