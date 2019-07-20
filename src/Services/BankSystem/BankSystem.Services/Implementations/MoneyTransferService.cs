﻿namespace BankSystem.Services.Implementations
{
    using AutoMapper;
    using AutoMapper.QueryableExtensions;
    using BankSystem.Models;
    using Common;
    using Common.EmailSender.Interface;
    using Data;
    using Interfaces;
    using Microsoft.EntityFrameworkCore;
    using Models.MoneyTransfer;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class MoneyTransferService : BaseService, IMoneyTransferService
    {
        private readonly IEmailSender emailSender;

        public MoneyTransferService(BankSystemDbContext context, IEmailSender emailSender)
            : base(context)
        {
            this.emailSender = emailSender;
        }

        public async Task<IEnumerable<T>> GetMoneyTransferAsync<T>(string referenceNumber)
            where T : MoneyTransferBaseServiceModel
            => await this.Context
                .Transfers
                .Where(t => t.ReferenceNumber == referenceNumber)
                .ProjectTo<T>()
                .ToArrayAsync();

        public async Task<int> GetCountOfAllMoneyTransfersForUserAsync(string userId)
            => await this.Context
                .Transfers
                .CountAsync(t => t.Account.UserId == userId);

        public async Task<IEnumerable<T>> GetMoneyTransfersAsync<T>(string userId, int pageIndex = 1, int count = int.MaxValue)
            where T : MoneyTransferBaseServiceModel
            => await this.Context
                .Transfers
                .Where(t => t.Account.UserId == userId)
                .OrderByDescending(mt => mt.MadeOn)
                .Skip((pageIndex - 1) * count)
                .Take(count)
                .ProjectTo<T>()
                .ToArrayAsync();

        public async Task<int> GetCountOfAllMoneyTransfersForAccountAsync(string accountId)
            => await this.Context
                .Transfers
                .CountAsync(t => t.AccountId == accountId);

        public async Task<IEnumerable<T>> GetMoneyTransfersForAccountAsync<T>(string accountId, int pageIndex = 1, int count = int.MaxValue)
            where T : MoneyTransferBaseServiceModel
            => await this.Context
                .Transfers
                .Where(t => t.AccountId == accountId)
                .OrderByDescending(mt => mt.MadeOn)
                .Skip((pageIndex - 1) * count)
                .Take(count)
                .ProjectTo<T>()
                .ToArrayAsync();

        public async Task<IEnumerable<T>> GetLast10MoneyTransfersForUserAsync<T>(string userId)
            where T : MoneyTransferBaseServiceModel
            => await this.Context
                .Transfers
                .Where(mt => mt.Account.UserId == userId)
                .OrderByDescending(mt => mt.MadeOn)
                .Take(10)
                .ProjectTo<T>()
                .ToArrayAsync();

        public async Task<bool> CreateMoneyTransferAsync<T>(T model)
            where T : MoneyTransferBaseServiceModel
        {
            if (!this.IsEntityStateValid(model))
            {
                return false;
            }

            var dbModel = Mapper.Map<MoneyTransfer>(model);
            var userAccount = await this.Context
                .Accounts
                .Include(u => u.User)
                .Where(u => u.Id == dbModel.AccountId)
                .SingleOrDefaultAsync();
            if (userAccount == null)
            {
                return false;
            }

            userAccount.Balance += dbModel.Amount;
            this.Context.Update(userAccount);

            await this.Context.Transfers.AddAsync(dbModel);
            await this.Context.SaveChangesAsync();

            if (dbModel.Amount > 0)
            {
                await this.emailSender.SendEmailAsync(dbModel.Account.User.Email, EmailMessages.ReceiveMoneySubject,
                    string.Format(EmailMessages.ReceiveMoneyMessage, dbModel.Amount));
            }
            else
            {
                await this.emailSender.SendEmailAsync(dbModel.Account.User.Email, EmailMessages.SendMoneySubject,
                    string.Format(EmailMessages.SendMoneyMessage, Math.Abs(dbModel.Amount)));
            }

            return true;
        }
    }
}