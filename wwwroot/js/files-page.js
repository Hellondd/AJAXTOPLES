// Тонкий UI поверх тонкого клиента.
// Здесь НЕТ логики HTTP — вся она в files-client.js.
// Вынесен в отдельный файл, потому что CSP (script-src 'self') запрещает inline-скрипты.
document.getElementById('uploadBtn').addEventListener('click', async () => {
    const file = document.getElementById('fileInput').files[0];
    const apiKey = document.getElementById('apiKey').value;
    const result = document.getElementById('result');
    const progress = document.getElementById('progress');

    result.textContent = '';
    try {
        const res = await FilesClient.upload(file, {
            apiKey,
            onProgress: p => progress.value = p
        });
        result.textContent = 'Загружено: ' + res.data.id;
    } catch (e) {
        // Обработка HTTP-границ: 401 — ре-аутентификация, 4xx — показать пользователю, 5xx — retry.
        if (e.code === 'unauthorized') {
            result.textContent = 'Ключ не принят. Проверьте X-Api-Key.';
        } else if (e.code === 'validation_failed') {
            result.textContent = 'Файл отклонён: ' + e.message;
        } else {
            result.textContent = `[${e.code}] ${e.message} (req ${e.correlationId})`;
        }
    }
});
