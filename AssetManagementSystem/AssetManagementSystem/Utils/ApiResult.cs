namespace AssetManagementSystem.Utils
{
	public class ApiResult<T>
	{
		public int? Code {  get; set; }
		public string? Message {  get; set; }
		public T? Data {  get; set; }
		public ApiResult() { }

		public ApiResult(int code, string message, T data)
		{
			Code = code;
			Message = message;
			Data = data;
		}

		public static ApiResult<T> Success(T data, string message = "操作成功")
		{
			return new ApiResult<T> { Code = 200, Message = message, Data = data };
		}

		/// <summary>
		/// 构造失败结果
		/// </summary>
		/// <param name="code">业务错误码（如 400 参数错误 / 401 未授权 / 403 禁用 / 404 不存在）</param>
		/// <param name="message">错误提示</param>
		public static ApiResult<T> Fail(string code, string message)
		{
			// 解析失败时兜底为 500（服务端错误）
			var codeValue = 500;
			if (!string.IsNullOrWhiteSpace(code) && int.TryParse(code, out var parsed))
			{
				codeValue = parsed;
			}

			return new ApiResult<T> { Code = codeValue, Message = message, Data = default(T) };
		}
	}
}
