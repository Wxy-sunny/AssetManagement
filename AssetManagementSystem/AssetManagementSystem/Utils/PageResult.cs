using System;
using System.Collections.Generic;

namespace AssetManagementSystem.Utils
{
	/// <summary>
	/// 泛型分页结果
	/// </summary>
	public class PageResult<T>
	{
		/// <summary>当前页数据</summary>
		public List<T> list { get; set; } = new List<T>();

		/// <summary>总记录数</summary>
		public int total { get; set; }

		/// <summary>当前页码（从 1 开始）</summary>
		public int pageIndex { get; set; }

		/// <summary>每页条数</summary>
		public int pageSize { get; set; }

		/// <summary>总页数</summary>
		public int totalPages => pageSize > 0 ? (int)Math.Ceiling(total * 1.0 / pageSize) : 0;

		public PageResult()
		{
		}

		public PageResult(List<T> list, int total, int pageIndex, int pageSize)
		{
			this.list = list;
			this.total = total;
			this.pageIndex = pageIndex;
			this.pageSize = pageSize;
		}
	}
}
