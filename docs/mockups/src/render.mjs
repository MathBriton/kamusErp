// Renderiza cada <section class="screen"> de mockups.html em PNG e monta o board geral.
// Uso: node docs/mockups/src/render.mjs
import { chromium } from '/opt/node-tools/node_modules/playwright/index.mjs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import fs from 'node:fs';

const dir = path.dirname(fileURLToPath(import.meta.url));
const out = path.resolve(dir, '..');
const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1500, height: 1000 }, deviceScaleFactor: 1 });
await page.goto('file://' + path.join(dir, 'mockups.html'));
await page.evaluate(() => document.fonts.ready);
const ids = await page.$$eval('section.screen', s => s.map(e => e.id));
for (const id of ids) {
  await page.locator(`[id="${id}"]`).screenshot({ path: path.join(out, id + '.png') });
  console.log('ok', id);
}

// Board estilo Figma com todas as telas
const labels = {
  '01-login': 'Login', '02-dashboard-claro': 'Dashboard · claro', '03-dashboard-escuro': 'Dashboard · escuro',
  '04-pesquisa-global': 'Pesquisa global (Ctrl+K)', '05-seletor-empresa': 'Troca de CNPJ', '06-notificacoes-escuro': 'Notificações · escuro',
  '07-produto-grade': 'Produto · grade cor × tamanho', '08-contas-pagar-exportar': 'Contas a pagar · exportação · carência',
  '09-licenca-suspensa': 'Licença suspensa', '10-admin-licencas': 'Painel Admin · licenças', '11-mobile': 'Mobile (somente leitura)',
};
const frames = ids.map(id => `<div class="f"><div class="lb"><span>#</span>${id.slice(0, 2)} · ${labels[id] ?? id}</div>
  <img src="data:image/png;base64,${fs.readFileSync(path.join(out, id + '.png')).toString('base64')}"></div>`).join('');
await page.setViewportSize({ width: 2400, height: 1200 });
await page.setContent(`<html><body style="margin:0;background:#E9E9EC;font-family:Inter">
 <div style="height:52px;background:#2C2C2C;color:#fff;display:flex;align-items:center;gap:14px;padding:0 24px;font-size:14px">
  <b>KamusERP</b><span style="opacity:.5">/</span><span>Frontend — v0.1 (levantamento)</span><span style="margin-left:auto;opacity:.6">${ids.length} frames · 1440×900</span></div>
 <div id="c" style="display:grid;grid-template-columns:repeat(3,720px);gap:56px 48px;padding:48px 60px 64px">${frames}</div>
 <style>.f .lb{font-size:15px;color:#555;margin-bottom:10px;font-weight:500}.f .lb span{color:#8B85FF;margin-right:6px}
 .f img{width:720px;display:block;border-radius:6px;box-shadow:0 2px 12px rgba(0,0,0,.12)}</style></body></html>`);
await page.evaluate(() => Promise.all([...document.images].map(i => i.decode())));
await page.screenshot({ path: path.join(out, '00-board.png'), fullPage: true });
console.log('ok board');
await browser.close();
