using System;
using AssetManagementSystem.Models;

namespace AssetManagementSystem.Services
{
	/// <summary>
	/// 折旧计算服务：支持不计提 / 平均年限法 / 年数总和法 / 双倍余额递减法
	/// 计算结果直接回写到实体字段（月折旧额、累计折旧、账面净值等）
	/// </summary>
	public static class DepreciationService
	{
		/// <summary>
		/// 计算折旧并回写实体
		/// </summary>
		/// <param name="entity">资产实体</param>
		/// <param name="asOfDate">计算基准日期，默认今天</param>
		public static void Calculate(Asset entity, DateTime? asOfDate = null)
		{
			if (entity == null) return;

			var asOf = asOfDate ?? DateTime.Now.Date;
			var original = entity.OriginalValue ?? 0m;
			var method = string.IsNullOrWhiteSpace(entity.DepreciationMethod)
				? DepreciationMethods.None
				: entity.DepreciationMethod;
			var lifeMonths = entity.UsefulLifeMonths ?? 0;
			var rate = entity.SalvageRate ?? 0m;

			// 不计提折旧 / 无原值 / 无使用年限：净值 = 原值
			if (method == DepreciationMethods.None || original <= 0 || lifeMonths <= 0)
			{
				entity.SalvageValue = 0;
				entity.MonthlyDepreciation = 0;
				entity.UsedMonths = 0;
				entity.AccumulatedDepreciation = 0;
				entity.NetValue = original;
				return;
			}

			// 预计净残值 & 应计折旧总额
			var salvage = Math.Round(original * rate / 100m, 2);
			var depreciable = Math.Max(original - salvage, 0m);
			entity.SalvageValue = salvage;

			// 折旧起始日期默认取采购日期
			var start = entity.DepreciationStartDate ?? entity.PurchaseDate ?? asOf;

			// 已计提月数，封顶到使用年限
			var passed = DiffMonths(start, asOf);
			var used = Math.Max(0, Math.Min(passed, lifeMonths));
			entity.UsedMonths = used;

			// 月折旧额（平均年限法下的固定月额）
			entity.MonthlyDepreciation = Math.Round(depreciable / lifeMonths, 2);

			decimal accumulated;
			switch (method)
			{
				case DepreciationMethods.SumOfYears:
					accumulated = depreciable * SumOfYearsRatio(used, lifeMonths);
					break;
				case DepreciationMethods.DoubleDeclining:
					accumulated = depreciable * DoubleDecliningRatio(used, lifeMonths);
					break;
				default:
					// 平均年限法 & 工作量法（工作量法暂按直线法折算）
					accumulated = depreciable * ((decimal)used / lifeMonths);
					break;
			}

			// 累计折旧不得超过应计折旧总额
			accumulated = Math.Min(Math.Round(accumulated, 2), depreciable);
			entity.AccumulatedDepreciation = accumulated;
			entity.NetValue = Math.Round(original - accumulated, 2);
		}

		/// <summary>
		/// 计算两个日期之间相差的月数（不足一月不计）
		/// </summary>
		private static int DiffMonths(DateTime start, DateTime end)
		{
			if (end <= start) return 0;
			var months = (end.Year - start.Year) * 12 + (end.Month - start.Month);
			if (end.Day < start.Day) months -= 1;
			return months < 0 ? 0 : months;
		}

		/// <summary>
		/// 年数总和法：已计提折旧占应计折旧总额的比例
		/// 第 k 年折旧率 = (n - k + 1) / [n(n+1)/2]
		/// </summary>
		private static decimal SumOfYearsRatio(int usedMonths, int lifeMonths)
		{
			var n = (int)Math.Ceiling(lifeMonths / 12m); // 总年数
			if (n <= 0) return 0m;

			var sum = n * (n + 1) / 2m;
			if (sum == 0) return 0m;

			decimal ratio = 0m;
			var remain = usedMonths;
			for (var k = 1; k <= n && remain > 0; k++)
			{
				var monthsInYear = Math.Min(12, remain);
				var yearRate = (n - k + 1) / sum;
				ratio += yearRate * (monthsInYear / 12m);
				remain -= monthsInYear;
			}
			return ratio;
		}

		/// <summary>
		/// 双倍余额递减法：已计提折旧占应计折旧总额的比例
		/// 年折旧率 = 2 / n，按年递减的账面价值计算
		/// </summary>
		private static decimal DoubleDecliningRatio(int usedMonths, int lifeMonths)
		{
			var n = (int)Math.Ceiling(lifeMonths / 12m); // 总年数
			if (n <= 0) return 0m;

			var rate = 2m / n;       // 年折旧率
			decimal remainRatio = 1m; // 剩余未折旧比例
			decimal acc = 0m;
			var remain = usedMonths;

			for (var k = 1; k <= n && remain > 0; k++)
			{
				var monthsInYear = Math.Min(12, remain);
				var yearDep = remainRatio * rate;
				acc += yearDep * (monthsInYear / 12m);
				remainRatio -= yearDep;
				if (remainRatio < 0) remainRatio = 0;
				remain -= monthsInYear;
			}
			return acc;
		}
	}
}
