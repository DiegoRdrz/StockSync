import {
  IconArrowsExchange, IconBell, IconBox, IconBuildingWarehouse, IconCategory, IconChartBar,
  IconLayoutDashboard, IconSettings, IconShoppingCart, IconTruckDelivery, IconUsers,
  IconAddressBook, type Icon,
} from '@tabler/icons-react';

export interface NavItem {
  label: string;
  to: string;
  icon: Icon;
  ownerOnly?: boolean;
  description: string;
}

export interface NavSection {
  label: string;
  items: NavItem[];
}

export const NAV: NavSection[] = [
  {
    label: 'General',
    items: [{ label: 'Resumen', to: '/', icon: IconLayoutDashboard, description: 'Ventas, stock bajo y actividad reciente' }],
  },
  {
    label: 'Inventario',
    items: [
      { label: 'Stock por sucursal', to: '/stock', icon: IconBuildingWarehouse, description: 'Existencias, entradas y salidas' },
      { label: 'Productos', to: '/products', icon: IconBox, description: 'Catálogo, precios y mínimos' },
      { label: 'Categorías', to: '/categories', icon: IconCategory, description: 'Agrupación del catálogo' },
      { label: 'Movimientos', to: '/movements', icon: IconArrowsExchange, description: 'Historial de entradas, salidas y ajustes' },
    ],
  },
  {
    label: 'Operación',
    items: [
      { label: 'Ventas', to: '/orders', icon: IconShoppingCart, description: 'Órdenes de venta por sucursal' },
      { label: 'Compras', to: '/purchase-orders', icon: IconTruckDelivery, ownerOnly: true, description: 'Órdenes de compra a proveedores' },
    ],
  },
  {
    label: 'Análisis',
    items: [
      { label: 'Reportes', to: '/reports', icon: IconChartBar, description: 'Ventas, compras y movimientos por período' },
      { label: 'Notificaciones', to: '/notifications', icon: IconBell, description: 'Alertas enviadas por correo y WhatsApp' },
      { label: 'Destinatarios', to: '/notifications/recipients', icon: IconAddressBook, description: 'Quién recibe las alertas' },
    ],
  },
  {
    label: 'Cuenta',
    items: [
      { label: 'Usuarios', to: '/users', icon: IconUsers, description: 'Equipo y roles' },
      { label: 'Configuración', to: '/settings', icon: IconSettings, description: 'Empresa, sucursales y contraseña' },
    ],
  },
];

export const ALL_NAV_ITEMS = NAV.flatMap((s) => s.items);
