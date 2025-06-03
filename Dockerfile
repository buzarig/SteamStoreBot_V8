# ------------------ BUILD STAGE ------------------
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

# 1) Копіюємо csproj-файл і відновлюємо залежності
COPY ["SteamStoreBot.csproj", "./"]
RUN dotnet restore "./SteamStoreBot.csproj"

# 2) Копіюємо решту файлів проєкту
COPY . .

# 3) Публікуємо у Release-конфігурації
RUN dotnet publish "SteamStoreBot.csproj" -c Release -o /app/publish

# ------------------ RUNTIME STAGE ------------------
FROM mcr.microsoft.com/dotnet/runtime:7.0 AS runtime
WORKDIR /app

# 4) Копіюємо зібрані артефакти з попереднього етапу
COPY --from=build /app/publish ./ 

# 5) Оскільки бот читає конфігурацію з botConfig.json, переконаємося, що файл присутній у фінальному образі.
# Якщо ви хочете підкладати його через змінні оточення, цю копію можна прибрати, 
# але якщо він містить лише шаблон (без токена), можна лишити.

# 6) Вказуємо команду запуску
ENTRYPOINT ["dotnet", "SteamStoreBot.dll"]
