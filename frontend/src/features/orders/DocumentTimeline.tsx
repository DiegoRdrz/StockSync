import { Text, Timeline } from '@mantine/core';
import { IconCheck } from '@tabler/icons-react';
import { formatDateTime } from '@/lib/format';

export interface TimelineEvent {
  label: string;
  date: string | null;
  color?: string;
}

/** Only events that already happened are shown, oldest first. */
export function DocumentTimeline({ events }: { events: TimelineEvent[] }) {
  const done = events.filter((e) => e.date).sort((a, b) => a.date!.localeCompare(b.date!));
  return (
    <Timeline active={done.length - 1} bulletSize={20} lineWidth={2} color="pine" aria-label="Historial">
      {done.map((event) => (
        <Timeline.Item
          key={event.label}
          color={event.color}
          bullet={<IconCheck size={12} />}
          title={<Text size="sm" fw={500}>{event.label}</Text>}
        >
          <Text size="xs" c="dimmed" className="tabular">{formatDateTime(event.date)}</Text>
        </Timeline.Item>
      ))}
    </Timeline>
  );
}
