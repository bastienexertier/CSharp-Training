﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TP.BankLib.model;
using TP.Client;

namespace TP.BankLib
{
	public class Bank
	{
		private IDictionary<string, IBankAccountInfo> _accounts;
		private IDictionary<IPerson, IList<IBankAccountInfo>> _accountsByOwner;
		private IDictionary<string, TransactionHistory> _histories;

		private PositiveDouble _savingAccountDailyInterest;
		private TimeSpan _savingAccountDebitLockDuration;

		public Bank(PositiveDouble savingAccountDailyInterest, TimeSpan savingAccountDebitLockDuration)
		{
			_accounts = new Dictionary<string, IBankAccountInfo>();
			_accountsByOwner = new Dictionary<IPerson, IList<IBankAccountInfo>>();
			_histories = new Dictionary<string, TransactionHistory>();

			_savingAccountDailyInterest = savingAccountDailyInterest;
			_savingAccountDebitLockDuration = savingAccountDebitLockDuration;
		}

		private IBankAccountInfo AddAccountToBank(IBankAccountInfo account)
		{
			_accounts[account.Numero] = account;

			_histories[account.Numero] = new TransactionHistory(10);

			if (!_accountsByOwner.ContainsKey(account.Owner))
			{
				_accountsByOwner[account.Owner] = new List<IBankAccountInfo>();
			}
			_accountsByOwner[account.Owner].Add(account);
			return account;
		}

		public IBankAccountInfo OpenBankAccount(string number, IPerson owner, DateTime now, PositiveDouble initialCredit)
		{
			IBankAccountInfo account = new BankAccountInfo(number, owner, now, initialCredit);
			return AddAccountToBank(account);
		}

		public ISavingAccountInfo OpenSavingAccount(string number, IPerson owner, DateTime now, PositiveDouble initialCredit)
		{
			ISavingAccountInfo account = new SavingAccountInfo(
				new BankAccountInfo(number, owner, now, initialCredit),
				_savingAccountDailyInterest,
				_savingAccountDebitLockDuration
			);
			AddAccountToBank(account);
			return account;
		}

		private IEnumerable<IBankAccountInfo> AccountsInfo {
			get {
				return _accounts.Values.AsEnumerable();
			}
		}

		public IEnumerable<IBankAccountInfo> AccountInfoWhereSoldeIsGreaterThan(PositiveDouble amount)
		{
			AccountsInfo.ToList().RemoveAll(account => account.Solde < amount);
			return AccountsInfo.Where<IBankAccountInfo>(account => account.Solde > amount);
		}

		public IBankAccountOperations GetOperationForAccount(string number)
		{
			if (!_accounts.TryGetValue(number, out IBankAccountInfo account))
			{
				return null;
			}

			IBankAccountOperations operations = new BankAccountOperationsWithHistory(
				new BankAccountOperations(_accounts[number]), _histories[number]
			);

			if (account is ISavingAccountInfo savingAccount)
			{
				operations = new SavingAccountOperations(operations, savingAccount);
			}

			return operations;
		}

		public TransactionHistory GetHistoryForAccount(string number)
		{
			if (_histories.ContainsKey(number))
			{
				return _histories[number];
			}
			return null;
		}

		public bool DebitFee(DateTime now, PositiveDouble feeAmount)
		{
			return (
				AccountsInfo
				.Where(account => !(account is SavingAccountInfo))
				.Select(account => new BankAccountOperations(account))
				.Select(operations => operations.Debiter(now, feeAmount))
				.ToArray()
				.All(success => success)
			);
		}

		public PositiveDouble ComputeTotalInterest(DateTime now)
		{
			return new PositiveDouble(
				AccountsInfo
				.OfType<ISavingAccountInfo>()
				.Select(account => new SavingAccountOperations(null, account))
				.Select(operations => operations.ComputeInterestDue(now))
				.Select(interestDue => interestDue.Value)
				.Sum()
			 );
		}

		public bool CreditInterestForAllSavingAccounts(DateTime now)
		{
			return (
				AccountsInfo
				.OfType<ISavingAccountInfo>()
				.Select(accountinfo => new SavingAccountOperations(GetOperationForAccount(accountinfo.Numero), accountinfo))
				.Select(operations => operations.CreditInterestDue(now))
				.ToArray()
				.All(success => success)
			);
		}

		public IEnumerable<IBankAccountInfo> GetAccountsWithSoldeAbove(PositiveDouble amount)
        {
			return (
				AccountsInfo
				.Where(account => account.Solde >= amount)
				.OrderBy(account => account.Owner.Nom)
			);
        }

		public IPerson GetOwnerOfBiggestAccount()
        {
			return (
				AccountsInfo
				.OrderBy(account => account.Solde)
				.First()
				.Owner
			);
        }

		public override string ToString()
		{
			return new StringBuilder().AppendJoin('\n', AccountsInfo).ToString();
		}

		public PositiveDouble GetTotalSolde()
		{
			return new PositiveDouble(AccountsInfo.Select(account => account.Solde.Value).Sum());
		}

		public async Task<PositiveDouble> GetTotalSoldeAsync()
		{
			IEnumerable<double> soldes = await Task.WhenAll(
                AccountsInfo.Select(async account => (await account.GetSoldeAsync()).Value)
			);
			return new PositiveDouble(soldes.Sum());
		}
	}
}
