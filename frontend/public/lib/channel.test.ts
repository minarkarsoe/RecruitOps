import { describe, it, expect } from 'vitest';
import { resolveChannel, tagged } from './channel';

const PAGE = 'https://careers.example.com/jobs/tok1';

describe('resolveChannel (ADR-0032 §1)', () => {
  it.each([
    ['facebook', 'Facebook'],
    ['LinkedIn', 'LinkedIn'],
    [' TELEGRAM ', 'Telegram'],
    ['viber', 'Viber'],
    ['referral', 'Referral'],
  ])('takes the link\'s via=%s tag first', (via, expected) => {
    expect(resolveChannel(`${PAGE}?via=${encodeURIComponent(via)}`, 'https://t.me/')).toBe(expected);
  });

  it('ignores a tag it does not know, and falls through to the referrer', () => {
    expect(resolveChannel(`${PAGE}?via=manualupload`, 'https://www.linkedin.com/feed/')).toBe('LinkedIn');
    expect(resolveChannel(`${PAGE}?via=constructor`, '')).toBeNull();
  });

  it('reads Facebook\'s own click tag', () => {
    expect(resolveChannel(`${PAGE}?fbclid=IwAR0abc`, '')).toBe('Facebook');
  });

  it.each([
    ['https://l.facebook.com/', 'Facebook'],
    ['https://m.facebook.com/groups/1', 'Facebook'],
    ['https://fb.me/x', 'Facebook'],
    ['https://www.linkedin.com/feed/', 'LinkedIn'],
    ['https://lnkd.in/abc', 'LinkedIn'],
    ['https://t.me/somechannel', 'Telegram'],
    ['https://web.telegram.org/k/', 'Telegram'],
    ['https://www.viber.com/', 'Viber'],
  ])('maps a referrer of %s', (referrer, expected) => {
    expect(resolveChannel(PAGE, referrer)).toBe(expected);
  });

  it.each([
    'https://notfacebook.com/',
    'https://facebook.com.example.net/',
    'https://careers.example.com/jobs/tok1',
    'not a url',
    '',
  ])('is the career page from %j', (referrer) => {
    expect(resolveChannel(PAGE, referrer)).toBeNull();
  });
});

describe('tagged', () => {
  it('adds the channel, replacing one the address already had', () => {
    expect(tagged(PAGE, 'viber')).toBe(`${PAGE}?via=viber`);
    expect(tagged(`${PAGE}?via=facebook`, 'telegram')).toBe(`${PAGE}?via=telegram`);
  });
});
