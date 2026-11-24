import dayjs from 'dayjs';
import 'dayjs/locale/es';
import relativeTime from 'dayjs/plugin/relativeTime';
import utc from 'dayjs/plugin/utc';

dayjs.extend(relativeTime);
dayjs.extend(utc);
dayjs.locale('es');

// Prices are decimal(2) in the backend: show cents only when they exist.
const currency = new Intl.NumberFormat('es-CO', {
  style: 'currency', currency: 'COP', minimumFractionDigits: 0, maximumFractionDigits: 2,
});
const currencyCompact = new Intl.NumberFormat('es-CO', {
  style: 'currency', currency: 'COP', notation: 'compact', maximumFractionDigits: 1,
});
const integer = new Intl.NumberFormat('es-CO', { maximumFractionDigits: 0 });

export const formatMoney = (value: number | null | undefined) => currency.format(value ?? 0);
export const formatMoneyCompact = (value: number | null | undefined) => currencyCompact.format(value ?? 0);
export const formatNumber = (value: number | null | undefined) => integer.format(value ?? 0);

export const formatDate = (value: string | null | undefined) =>
  value ? dayjs(value).format('D MMM YYYY') : '—';
export const formatDateTime = (value: string | null | undefined) =>
  value ? dayjs(value).format('D MMM YYYY, h:mm a') : '—';
export const formatRelative = (value: string | null | undefined) => (value ? dayjs(value).fromNow() : '—');
/** Reporting returns UTC days; show them without shifting to local time. */
export const formatDay = (value: string) => dayjs.utc(value).format('D MMM');

/** `yyyy-MM-dd` string, the value format of Mantine date pickers. */
export const toIsoDay = (value: Date | string | null | undefined) =>
  value ? dayjs(value).format('YYYY-MM-DD') : undefined;

/*
 * Filter bounds sent to the APIs. A bare `to=yyyy-MM-dd` means 00:00Z and would
 * exclude that whole day, so days picked by the user (local calendar days) are
 * sent as UTC instants covering the full local day.
 */
export const dayStartUtc = (day: Date | string | null | undefined) =>
  day ? dayjs(day).startOf('day').toISOString() : undefined;
export const dayEndUtc = (day: Date | string | null | undefined) =>
  day ? dayjs(day).endOf('day').toISOString() : undefined;

export const shortId = (id: string) => id.slice(0, 8);

export { dayjs };
