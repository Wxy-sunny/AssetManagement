using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AssetManagementSystem.Dtos;
using AssetManagementSystem.Utils;
using Xunit;

namespace AssetManagementSystem.Tests;

public class TreeHelperTests
{
	private static CategoryDto Node(string id, string? parentId)
		=> new CategoryDto { id = id, parentId = parentId ?? string.Empty };

	private static List<CategoryDto> Build(List<CategoryDto> nodes)
		=> TreeHelper.BuildTree(
			nodes,
			d => d.id ?? string.Empty,
			d => d.parentId,
			(p, c) => p.children = c,
			rootParentId: null);

	[Fact]
	public void Empty_ReturnsEmpty()
	{
		Assert.Empty(Build(new List<CategoryDto>()));
	}

	[Fact]
	public void SimpleTree_BuildsHierarchy()
	{
		var nodes = new List<CategoryDto>
		{
			Node("root", null),
			Node("a", "root"),
			Node("b", "root"),
			Node("a1", "a"),
		};

		var tree = Build(nodes);

		Assert.Single(tree);
		Assert.Equal(2, tree[0].children.Count);
		Assert.Single(tree[0].children.Single(c => c.id == "a").children);
	}

	[Fact]
	public void CircularReference_DoesNotCrashOrHang()
	{
		// A 的父级是 B、B 的父级是 A（互为父级）：老实现会无限递归导致进程崩溃
		var nodes = new List<CategoryDto>
		{
			Node("root", null),
			Node("a", "root"),
			Node("b", "a"),
			Node("c", "b"),
			Node("c2", "c"), // 正常链
			Node("x", "y"),
			Node("y", "x"),  // 环
		};

		var tree = Build(nodes);

		// 环不参与输出（无根可达），但不能抛异常/栈溢出
		Assert.Single(tree);
		var a = tree[0].children.Single(c => c.id == "a");
		Assert.Equal("b", a.children[0].id);
	}

	[Fact]
	public void DeepChain_BuildsWithoutStackOverflow()
	{
		const int depth = 500;
		var nodes = new List<CategoryDto> { Node("n0", null) };
		for (var i = 1; i < depth; i++)
			nodes.Add(Node($"n{i}", $"n{i - 1}"));

		var tree = Build(nodes);

		Assert.Single(tree);
		var cur = tree[0];
		var count = 1;
		while (cur.children.Count > 0)
		{
			cur = cur.children[0];
			count++;
		}
		Assert.Equal(depth, count);
	}

	[Fact]
	public void LargeDataset_CompletesQuickly()
	{
		// 老实现对每个父节点全表扫描，是 O(n²)：5000 节点时 CPU 打满。
		// 此用例作为性能回归护栏：正常应远小于 1 秒。
		var nodes = new List<CategoryDto>();
		const int perLevel = 5000;
		for (var i = 0; i < perLevel; i++)
			nodes.Add(Node($"n{i}", null));
		// 每个根下挂一个子节点
		for (var i = 0; i < perLevel; i++)
			nodes.Add(Node($"c{i}", $"n{i}"));

		var sw = Stopwatch.StartNew();
		var tree = Build(nodes);
		sw.Stop();

		Assert.Equal(perLevel, tree.Count);
		Assert.True(sw.ElapsedMilliseconds < 2000, $"构建耗时 {sw.ElapsedMilliseconds}ms，疑似退化");
	}
}
