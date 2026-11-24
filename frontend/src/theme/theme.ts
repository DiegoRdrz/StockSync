import { createTheme, type MantineColorsTuple } from '@mantine/core';

// "Pine": primary action colour. Amber is reserved for low stock, red for errors.
const pine: MantineColorsTuple = [
  '#e8f6f2', '#d3ebe4', '#a6d6c8', '#76c0ab', '#4fad93',
  '#36a184', '#239a7b', '#11705f', '#0d6354', '#004f42',
];

const amber: MantineColorsTuple = [
  '#fff6e3', '#fdecce', '#f9d79d', '#f5c167', '#f2b544',
  '#f0a82a', '#efa21c', '#c77d12', '#b86f00', '#a05f00',
];

// Cool green-grey neutrals used for structure (dark scheme surfaces included).
const slate: MantineColorsTuple = [
  '#f5f7f6', '#e9edeb', '#d3dbd8', '#b7c3bf', '#8e9d98',
  '#5f6f6a', '#45534f', '#2c3733', '#1f2926', '#151c1a',
];

export const theme = createTheme({
  primaryColor: 'pine',
  primaryShade: { light: 7, dark: 5 },
  colors: { pine, amber, dark: [
    '#d3dbd8', '#b7c3bf', '#8e9d98', '#5f6f6a', '#3a4642',
    '#2a3431', '#1f2926', '#182120', '#121918', '#0d1312',
  ], gray: slate },
  fontFamily: '"IBM Plex Sans", system-ui, -apple-system, "Segoe UI", sans-serif',
  headings: {
    fontFamily: '"Bricolage Grotesque Variable", "IBM Plex Sans", system-ui, sans-serif',
    fontWeight: '600',
    sizes: {
      h1: { fontSize: '1.5rem', lineHeight: '1.25' },
      h2: { fontSize: '1.125rem', lineHeight: '1.3' },
      h3: { fontSize: '1.05rem', lineHeight: '1.3' },
    },
  },
  fontSizes: { xs: '0.75rem', sm: '0.8125rem', md: '0.875rem', lg: '1rem', xl: '1.125rem' },
  radius: { xs: '4px', sm: '6px', md: '8px', lg: '12px', xl: '16px' },
  defaultRadius: 'md',
  cursorType: 'pointer',
  focusRing: 'auto',
  components: {
    Button: { defaultProps: { fw: 550 } },
    Paper: { defaultProps: { withBorder: true, radius: 'md' } },
    Card: { defaultProps: { withBorder: true, radius: 'md' } },
    TextInput: { defaultProps: { size: 'sm' } },
    Select: { defaultProps: { size: 'sm', allowDeselect: false } },
    Table: { defaultProps: { verticalSpacing: 8, horizontalSpacing: 'sm', highlightOnHover: true } },
    Modal: { defaultProps: { centered: true, radius: 'lg', overlayProps: { blur: 2 } } },
    Tooltip: { defaultProps: { withArrow: true, openDelay: 250 } },
    Badge: { defaultProps: { variant: 'light', radius: 'sm', tt: 'none' }, styles: { label: { fontWeight: 600 } } },
  },
});
