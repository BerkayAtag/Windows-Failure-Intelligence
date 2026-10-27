import { test, expect } from '@playwright/test';
import os from 'node:os';
import path from 'node:path';
test('AI result exposes uncertainty and evidence safely in all themes', async ({ page }) => {
  const errors: string[] = []; page.on('pageerror', e => errors.push(e.message));
  const device = '11111111-1111-1111-1111-111111111111', job = '22222222-2222-2222-2222-222222222222';
  await page.route('**/api/v1/devices?*', route => route.fulfill({json:{items:[{id:device,hostname:'synthetic-evidence',domain:'synthetic.example',status:'online',agentVersion:'test',lastHeartbeat:'2026-10-25T00:00:00Z',automationMode:'Disabled'}],total:1}}));
  await page.route(`**/api/v1/devices/${device}/activity`, route => route.fulfill({json:{diagnostics:[{id:job,status:'completed',scanType:'NetworkScan'}],remediations:[]}}));
  let score: number | undefined = .7;
  await page.route(`**/api/v1/diagnostics/${job}`, route => route.fulfill({json:{status:'completed',analysis:{status:'completed',modelUsed:'synthetic-model',conclusion:'Sentetik değerlendirme',confidenceScore:score,hypothesesJson:JSON.stringify([{title:'DNS hipotezi',explanation:'Sentetik açıklama',evidenceCitations:['System/5719'],verificationSteps:['DNS yapılandırmasını doğrulayın.']}]),missingEvidenceJson:JSON.stringify(['<script>sentetik</script> DNS çözümleme sonucu']),recommendedPlaybooksJson:'[]'}}}));
  await page.goto('/'); await expect(page).toHaveTitle(/WFI/);
  await page.getByRole('button',{name:'Demoyu aç'}).click();
  await page.getByRole('button',{name:'Cihazlar ve AI',exact:true}).click();
  async function openResult(){await page.getByRole('button',{name:'Cihazı aç',exact:true}).click();await page.getByRole('button',{name:'Teşhis sonucu',exact:true}).click();}
  await openResult();
  await expect(page.getByText('Modelin bildirdiği güven: %70.',{exact:false})).toBeVisible();
  await expect(page.getByText('<script>sentetik</script> DNS çözümleme sonucu',{exact:true})).toBeVisible();
  await expect(page.getByText('DNS yapılandırmasını doğrulayın.',{exact:true})).toBeVisible();
  await expect(page.getByText('System/5719',{exact:true})).toBeVisible();
  for(const theme of ['standard','light','dark']){
    await page.getByLabel('Tema',{exact:true}).selectOption(theme);
    await expect(page.getByRole('heading',{name:'Eksik kanıtlar',exact:true})).toBeVisible();
  }
  await page.screenshot({path:path.join(os.tmpdir(),'wfi-ai-evidence-desktop.png'),fullPage:true});
  await page.setViewportSize({width:390,height:844});
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  await page.screenshot({path:path.join(os.tmpdir(),'wfi-ai-evidence-mobile.png'),fullPage:true});
  score=undefined;
  await page.getByRole('button',{name:'Cihazı kapat',exact:true}).click();await openResult();
  await expect(page.getByText('Modelin bildirdiği güven: Kullanılabilir değil.',{exact:false})).toBeVisible();
  expect(errors).toEqual([]);
});
