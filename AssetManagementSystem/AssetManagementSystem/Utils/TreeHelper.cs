namespace AssetManagementSystem.Utils
{
	/// <summary>
	/// 树形结构构建工具。
	/// 健壮性要点：
	/// 1) O(n) 分组构建（按父级分桶），原实现是"对每个父节点全表扫描"的 O(n²)，
	///    在极端数据（上万节点）下会把 CPU 打满；
	/// 2) visited 集合防止循环引用（A 的父级是 B、B 的父级是 A）导致无限递归崩溃进程；
	/// 3) 递归深度仍受调用栈限制，层级过深（数千层）时应改为迭代实现——正常业务不会出现。
	/// </summary>
	public class TreeHelper
	{
		/// <summary>
		/// 将平铺数据构建为树形结构
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
			if (list.Count == 0) return new List<T>();

			// O(n) 预处理：按"父级 Id"分桶（null 归入空串桶，与根判定一致）
			var childrenMap = new Dictionary<string, List<T>>();
			foreach (var n in list)
			{
				var pid = getParentId(n) ?? string.Empty;
				if (!childrenMap.TryGetValue(pid, out var bucket))
				{
					bucket = new List<T>();
					childrenMap[pid] = bucket;
				}
				bucket.Add(n);
			}

			// 找出所有根节点（ParentId 等于 rootParentId，或 rootParentId 为 null 时 ParentId 为空）
			var rootNodes = new List<T>();
			foreach (var n in list)
			{
				var pid = getParentId(n);
				bool isRoot = rootParentId != null
					? pid == rootParentId
					: string.IsNullOrEmpty(pid);
				if (isRoot) rootNodes.Add(n);
			}

			// 递归填充子节点（visited 打破循环引用，避免进程被递归栈炸掉）
			var visited = new HashSet<string>();
			foreach (var root in rootNodes)
			{
				FillChildren(root, childrenMap, getId, setChildren, visited);
			}

			return rootNodes;
		}

		private static void FillChildren<T>(
			T parent,
			Dictionary<string, List<T>> childrenMap,
			Func<T, string> getId,
			Action<T, List<T>> setChildren,
			HashSet<string> visited)
		{
			var parentId = getId(parent);

			// 已访问过的节点直接跳过：数据里若存在循环引用（互为父级），
			// 不打断会无限递归导致 StackOverflowException 使进程崩溃
			if (parentId == null || !visited.Add(parentId))
				return;

			if (childrenMap.TryGetValue(parentId, out var children) && children.Count > 0)
			{
				setChildren(parent, children);
				foreach (var child in children)
				{
					FillChildren(child, childrenMap, getId, setChildren, visited);
				}
			}
			else
			{
				setChildren(parent, new List<T>());
			}
		}
	}
}
