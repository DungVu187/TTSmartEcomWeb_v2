import {
  LAST_CUSTOMER_PATH_KEY,
  rememberCustomerPath,
} from './customerroutehistory.utils';

describe('rememberCustomerPath', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  it('remembers the storefront path including query and hash', () => {
    expect(rememberCustomerPath({
      pathname: '/product/abc',
      search: '?station=HN',
      hash: '#detail',
    })).toBe(true);

    expect(sessionStorage.getItem(LAST_CUSTOMER_PATH_KEY)).toBe(
      '/product/abc?station=HN#detail',
    );
  });

  it.each(['/admin', '/admin/product'])('never stores admin path %s', (pathname) => {
    sessionStorage.setItem(LAST_CUSTOMER_PATH_KEY, '/product');

    expect(rememberCustomerPath({ pathname })).toBe(false);
    expect(sessionStorage.getItem(LAST_CUSTOMER_PATH_KEY)).toBe('/product');
  });
});
