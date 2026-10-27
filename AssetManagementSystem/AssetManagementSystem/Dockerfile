# ============================================================
# 后端镜像（多阶段构建）
# 构建命令（在仓库根目录执行）：
#   docker build -f AssetManagementSystem/AssetManagementSystem/Dockerfile -t asset-api .
# 说明：构建上下文为仓库根，因此下面的 COPY 路径相对于仓库根。
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 复制整个仓库（前端目录被根 .dockerignore 排除；bin/obj 同样排除）
COPY . .

# 仅还原/发布后端项目（测试项目不参与发布）
RUN dotnet restore AssetManagementSystem/AssetManagementSystem/AssetManagementSystem.csproj
RUN dotnet publish AssetManagementSystem/AssetManagementSystem/AssetManagementSystem.csproj -c Release -o /app/publish --no-restore

# 运行阶段
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# 应用监听 5099（见 appsettings 中的配置）
ENV ASPNETCORE_URLS=http://+:5099
EXPOSE 5099

ENTRYPOINT ["dotnet", "AssetManagementSystem.dll"]
