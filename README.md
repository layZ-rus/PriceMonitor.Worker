# 🕷️ Price Monitor Bot (v1.0)

Фоновый сервис на C# (.NET 8), который отслеживает цены на товары и присылает уведомления в Telegram при их снижении. Проект создан для демонстрации навыков работы с фоновыми задачами, базами данных и веб-парсингом.

![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![.NET 8](https://img.shields.io/badge/.NET_8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![SQLite](https://img.shields.io/badge/SQLite-003B57?style=for-the-badge&logo=sqlite&logoColor=white)
![Telegram](https://img.shields.io/badge/Telegram-2CA5E0?style=for-the-badge&logo=telegram&logoColor=white)

---

## ✨ Возможности

- **Фоновый мониторинг**: Проверка цен запускается автоматически каждые 30 секунд (реализовано через `BackgroundService`).
- **Парсинг данных**: Извлечение цен с HTML-страниц с помощью библиотеки `HtmlAgilityPack`.
- **База данных**: Сохранение списка отслеживаемых товаров и пользователей в SQLite (Entity Framework Core).
- **Интерактивный Telegram-бот**: Управление списком прямо из чата.
- **Конвертация валют**: Автоматический перевод цен в рубли (для теста используется фиксированный курс).

---

## 🤖 Команды бота

- `/start` — Приветствие и краткая справка.
- `/list` — Посмотреть все добавленные товары и их текущие цены.
- `/add [ссылка]` — Добавить новый товар для отслеживания (пока поддерживается тестовый сайт `books.toscrape.com`).

---

## 🛠️ Стек технологий

- **Язык**: C# (.NET 8)
- **Telegram**: `Telegram.Bot`
- **База данных**: `Microsoft.EntityFrameworkCore.Sqlite`
- **Парсинг**: `HtmlAgilityPack`
- **Архитектура**: Worker Service (Hosted Service)

---

## 🚀 Как запустить локально

Так как конфиденциальные данные (токен бота) скрыты из репозитория, для запуска потребуется их добавить.

### Шаг 1. Клонирование репозитория
Откройте терминал и выполните команду:

```bash
git clone https://github.com/layZ-rus/PriceMonitor.Worker.git
```

### Шаг 2. Настройка конфигурации
В корневой папке проекта создайте файл `appsettings.json` (если его нет) и добавьте туда ваш токен от `@BotFather`:

```json
{
  "Telegram": {
    "BotToken": "ВАШ_ТОКЕН_ОТ_BOTFATHER"
  }
}
```

> ⚠️ **Внимание:** Убедитесь, что этот файл находится в `.gitignore`, чтобы случайно не закоммитить секреты в открытый доступ.

### Шаг 3. Запуск
Запустите проект в Visual Studio (F5). База данных `price_monitor.db` создастся автоматически при первом запуске.

---

## 📸 Как это работает

1. Отправьте боту команду:
```text
/add http://books.toscrape.com/catalogue/a-light-in-the-attic_1000/index.html
```
2. Бот спарсит название и цену, переведет её в рубли и сохранит в базу.
3. Если в процессе мониторинга цена упадет, бот пришлет вам уведомление в чат.

---

## 👨‍💻 Автор

**Игорь Романюк**
*   Telegram: [@layz102](https://t.me/layz102)
*   GitHub: [@layZ-rus](https://github.com/layZ-rus)
*   Сайт-портфолио: [layz-rus.github.io](https://layz-rus.github.io)

---
*Проект создан в учебных целях. В следующих версиях (v2.0+) планируется добавление поддержки Ozon, Wildberries и Яндекс.Маркета.*
