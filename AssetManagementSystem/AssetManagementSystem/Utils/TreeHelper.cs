namespace AssetManagementSystem.Utils
{
	public class TreeHelper
	{
		/// <summary>
		/// 将平铺数据构建为树形结构（递归）
		/// </summary>
		/// <typeparam name="T">节点类型</typeparam>
		/// <param name="nodes">平铺数据</param>
		/// <param name="getId">获取节点 Id 的委托</param>
		/// <param name="getParentId">获取父级 Id 的委托</param>
		/// <param name="setChildren">设置子节点集合的委托（将孩子赋值给父节点的 Children 属性）</param>
		/// <param name="rootParentId">根节点的父级标识（默认为 null 或空字符串）</param>
		/// <returns>根节点列表</returns>
		public static List<T> BuildTree<T>(
			IEnumerable<T> nodes,
			Func<T, string> getId,
			Func<T, string?> getParentId,
			Action<T, List<T>> setChildren,
			string? rootParentId = null)
		{
			if (nodes == null) return new List<T>();

			var list = nodes.ToList();
			if (!list.Any()) return new List<T>();

			// 先找出所有根节点（ParentId 等于 rootParentId 或 null/空）
			var rootNodes = list.Where(n =>
			{
				var pid = getParentId(n);
				if (rootParentId != null)
					return pid == rootParentId;
				else
					return string.IsNullOrEmpty(pid);
			}).ToList();

			// 递归填充子节点
			foreach (var root in rootNodes)
			{
				FillChildren(root, list, getId, getParentId, setChildren);
			}

			return rootNodes;
		}

		private static void FillChildren<T>(
			T parent,
			List<T> allNodes,
			Func<T, string> getId,
			Func<T, string?> getParentId,
			Action<T, List<T>> setChildren)
		{
			var parentId = getId(parent);
			var children = allNodes.Where(n => getParentId(n) == parentId).ToList();

			if (children.Any())
			{
				setChildren(parent, children);
				foreach (var child in children)
				{
					FillChildren(child, allNodes, getId, getParentId, setChildren);
				}
			}
			else
			{
				setChildren(parent, new List<T>());
			}
		}
	}
}
