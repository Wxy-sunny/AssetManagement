namespace AssetManagementSystem.Dtos;

/// <summary>
/// 设置当前用户头像：传入系统内置头像或自行上传后得到的相对地址
/// </summary>
public class SetAvatarDto
{
	public string? avatar { get; set; }
}
