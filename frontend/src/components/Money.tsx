import { Text, type TextProps } from '@mantine/core';
import { formatMoney } from '@/lib/format';

export function Money({ value, ...props }: TextProps & { value: number | null | undefined }) {
  return (
    <Text component="span" className="tabular" inherit {...props}>
      {formatMoney(value)}
    </Text>
  );
}
