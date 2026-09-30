using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

class TypingTest : Form
{
    static readonly string[] Russian =
    {
        "Утро выдалось тихим и солнечным. На кухне пахло свежим кофе, а за окном медленно просыпался город. Соседский кот сидел на подоконнике и внимательно следил за голубями, которые важно расхаживали по двору.",
        "Чтобы научиться быстро печатать, не нужно смотреть на клавиатуру. Пальцы должны сами находить нужные клавиши. Сначала это кажется трудным, но уже через пару недель регулярных тренировок скорость заметно растёт.",
        "Поезд отправлялся ровно в восемь. Мы пришли на вокзал заранее, купили в дорогу пирожков и долго искали свой вагон. Проводница улыбнулась, проверила билеты и пожелала нам счастливого пути.",
        "Хорошая книга похожа на разговор с умным собеседником. Она не торопит, не перебивает и всегда готова рассказать что-то новое. Иногда одна прочитанная страница меняет взгляд на привычные вещи.",
        "В субботу мы решили выбраться за город. Дорога шла через поля и небольшие деревни, где у каждого дома росли яблони. К обеду добрались до озера, развели костёр и до вечера слушали, как шумит вода.",
    };

    static readonly string[] English =
    {
        "The quick brown fox jumps over the lazy dog. This sentence contains every letter of the alphabet, which is why typists have used it for more than a hundred years to test keyboards and practice their skills.",
        "Good habits are built one small step at a time. If you practice typing for just ten minutes every day, you will be surprised how much faster and more accurate you become after only a few weeks.",
        "The train left the station exactly at eight. We found our seats, opened a bag of snacks and watched the city lights fade away as the fields and quiet villages rolled past the window.",
    };

    RichTextBox view;
    Label speedLabel, accuracyLabel, timeLabel, bestLabel, hintLabel;
    ComboBox languageBox;
    Button restartButton;
    Timer timer;
    string text;
    int position, errors, totalTyped;
    readonly HashSet<int> wrong = new HashSet<int>();
    DateTime? started;
    bool finished;
    readonly Random random = new Random();
    int lastIndex = -1;

    TypingTest()
    {
        Text = "TypingTest";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        ClientSize = new Size(700, 400);
        Font = new Font("Segoe UI", 10);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        speedLabel = Stat("0", "зн/мин", 12);
        accuracyLabel = Stat("100%", "точность", 182);
        timeLabel = Stat("0:00", "время", 352);

        languageBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(530, 16), Width = 158 };
        languageBox.Items.AddRange(new object[] { "Русский", "English" });
        languageBox.SelectedIndex = 0;
        languageBox.SelectedIndexChanged += (s, e) => { Restart(); view.Focus(); };
        Controls.Add(languageBox);
        restartButton = new Button { Text = "Новый текст (Esc)", Location = new Point(530, 48), Size = new Size(158, 30) };
        restartButton.Click += (s, e) => { Restart(); view.Focus(); };
        Controls.Add(restartButton);

        view = new RichTextBox
        {
            Location = new Point(12, 96), Size = new Size(676, 226), ReadOnly = true, BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 16), BackColor = Color.White, ScrollBars = RichTextBoxScrollBars.None, Cursor = Cursors.IBeam,
            TabStop = true, ShortcutsEnabled = false
        };
        view.MouseDown += (s, e) => { view.Focus(); MoveCaret(); };
        view.Enter += (s, e) => MoveCaret();
        var frame = new Panel { Location = new Point(6, 90), Size = new Size(688, 238), BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
        Controls.Add(view);
        Controls.Add(frame);
        view.BringToFront();

        hintLabel = new Label { Location = new Point(12, 336), Size = new Size(676, 22), ForeColor = Color.Gray, Text = "Просто начни печатать: таймер запустится с первой буквы. Ошибку можно исправить Backspace" };
        Controls.Add(hintLabel);
        bestLabel = new Label { Location = new Point(12, 362), Size = new Size(676, 26), ForeColor = Color.FromArgb(40, 110, 90), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
        Controls.Add(bestLabel);

        timer = new Timer { Interval = 200 };
        timer.Tick += (s, e) => UpdateStats();
        Shown += (s, e) => view.Focus();
        Restart();
    }

    Label Stat(string value, string caption, int x)
    {
        var v = new Label { Text = value, Location = new Point(x, 8), Size = new Size(160, 44), Font = new Font("Segoe UI", 22, FontStyle.Bold), ForeColor = Color.FromArgb(40, 90, 160) };
        Controls.Add(v);
        Controls.Add(new Label { Text = caption, Location = new Point(x + 3, 54), AutoSize = true, ForeColor = Color.Gray });
        return v;
    }

    string LanguageKey { get { return languageBox.SelectedIndex == 0 ? "ru" : "en"; } }

    void Restart()
    {
        var pool = languageBox.SelectedIndex == 0 ? Russian : English;
        int index;
        do index = random.Next(pool.Length); while (pool.Length > 1 && index == lastIndex);
        lastIndex = index;
        text = pool[index];
        position = errors = totalTyped = 0;
        wrong.Clear();
        started = null;
        finished = false;
        timer.Stop();
        view.Text = text;
        view.SelectAll();
        view.SelectionColor = Color.FromArgb(150, 150, 150);
        view.SelectionBackColor = Color.White;
        MoveCaret();
        speedLabel.Text = "0";
        accuracyLabel.Text = "100%";
        timeLabel.Text = "0:00";
        ShowBest();
    }

    void ShowBest()
    {
        using (var key = Registry.CurrentUser.OpenSubKey(@"Software\TypingTest"))
        {
            object best = key == null ? null : key.GetValue("best_" + LanguageKey);
            bestLabel.Text = best == null ? "" : "Твой рекорд: " + best + " зн/мин";
        }
    }

    void MoveCaret()
    {
        if (position < view.TextLength) view.Select(position, 0);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { Restart(); return true; }
        if (keyData == Keys.Back && !finished && position > 0 && view.Focused)
        {
            position--;
            wrong.Remove(position);
            PaintChar(position, Color.FromArgb(150, 150, 150), Color.White);
            MoveCaret();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (!view.Focused || finished || char.IsControl(e.KeyChar)) return;
        e.Handled = true;
        if (!started.HasValue) { started = DateTime.Now; timer.Start(); }

        char expected = text[position];
        bool ok = Same(e.KeyChar, expected);
        totalTyped++;
        if (!ok) { errors++; wrong.Add(position); }
        PaintChar(position, ok ? Color.FromArgb(30, 30, 30) : Color.Firebrick, ok ? Color.White : Color.FromArgb(255, 220, 220));
        position++;
        MoveCaret();
        UpdateStats();
        if (position >= text.Length) Finish();
    }

    // ё/е and different dashes/quotes are treated as the same key, like most typing trainers do.
    static bool Same(char typed, char expected)
    {
        if (typed == expected) return true;
        string a = Normalize(typed), b = Normalize(expected);
        return a == b;
    }

    static string Normalize(char c)
    {
        switch (c)
        {
            case 'ё': return "е";
            case 'Ё': return "Е";
            case '—': case '–': return "-";
            case '«': case '»': case '“': case '”': return "\"";
            case '’': return "'";
            default: return c.ToString();
        }
    }

    void PaintChar(int index, Color fore, Color back)
    {
        view.Select(index, 1);
        view.SelectionColor = fore;
        view.SelectionBackColor = back;
    }

    void UpdateStats()
    {
        if (!started.HasValue) return;
        var elapsed = DateTime.Now - started.Value;
        double minutes = Math.Max(elapsed.TotalMinutes, 1 / 60.0);
        int correct = position - wrong.Count;
        speedLabel.Text = ((int)(correct / minutes)).ToString();
        accuracyLabel.Text = (totalTyped == 0 ? 100 : (int)Math.Round(100.0 * (totalTyped - errors) / totalTyped)) + "%";
        timeLabel.Text = ((int)elapsed.TotalMinutes) + ":" + elapsed.Seconds.ToString("00");
    }

    void Finish()
    {
        finished = true;
        timer.Stop();
        UpdateStats();
        int speed = int.Parse(speedLabel.Text);
        string verdict = speed < 150 ? "Начало положено. Попробуй не смотреть на клавиатуру" :
                         speed < 250 ? "Средний уровень. Ещё немного практики!" :
                         speed < 350 ? "Хорошая скорость, быстрее большинства" :
                         speed < 450 ? "Отлично! Уровень опытного пользователя" : "Невероятно! Уровень профессионала";
        hintLabel.Text = "Готово: " + verdict + ". Esc — новый текст";
        int best = 0;
        using (var key = Registry.CurrentUser.CreateSubKey(@"Software\TypingTest"))
        {
            object v = key.GetValue("best_" + LanguageKey);
            if (v != null) best = (int)v;
            if (speed > best && int.Parse(accuracyLabel.Text.TrimEnd('%')) >= 90)
            {
                key.SetValue("best_" + LanguageKey, speed, RegistryValueKind.DWord);
                bestLabel.Text = "🏆 Новый рекорд: " + speed + " зн/мин!";
                return;
            }
        }
        ShowBest();
    }

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new TypingTest());
    }
}
