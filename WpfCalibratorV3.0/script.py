import subprocess

# === НАСТРОЙКИ ===
OWNER = "Porselet"
REPO = "WpfCalibratorV3.0"
OUTPUT_FILE = "github_files_list.txt"

# Список расширений файлов, которые мы ищем (в нижнем регистре)
ALLOWED_EXTENSIONS = {".cs", ".xaml", ".sln", ".csproj"}
# =================

print("🔄 Запрашиваем список файлов через GitHub CLI...")

# Вызываем официальную команду CLI для получения всех файлов репозитория
result = subprocess.run(
    ["gh", "repo", "view", f"{OWNER}/{REPO}", "--json", "items", "--template", "{{range .items}}{{.path}}\n{{end}}"],
    capture_output=True,
    text=True,
    encoding="utf-8"
)

# Если gh выдал ошибку (например, репозиторий не найден)
if result.returncode != 0:
    print("❌ Ошибка при работе с GitHub CLI:")
    print(result.stderr)
    exit()

# Получаем строки и убираем пустые
file_paths = [line.strip() for line in result.stdout.split("\n") if line.strip()]

file_counter = 0

with open(OUTPUT_FILE, "w", encoding="utf-8") as f:
    for file_path in file_paths:
        # Проверяем, заканчивается ли файл на нужное расширение
        if any(file_path.lower().endswith(ext) for ext in ALLOWED_EXTENSIONS):
            file_counter += 1
            
            # Записываем красивую веб-ссылку (ветку ставим 'master', как у тебя)
            f.write(f"https://github.com{OWNER}/{REPO}/blob/master/{file_path}\n")
            
            # После каждого пятого файла вставляем пустую строку
            if file_counter % 5 == 0:
                f.write("\n")

print(f"🎉 Успех! Отфильтровано файлов: {file_counter}. Результат сохранен в: {OUTPUT_FILE}")
