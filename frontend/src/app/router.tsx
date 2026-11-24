import { Center, Loader } from '@mantine/core';
import { lazy, Suspense, type ReactNode } from 'react';
import { createBrowserRouter, Navigate, useLocation } from 'react-router-dom';
import { EmptyState } from '@/components';
import { LoginPage } from '@/features/auth/LoginPage';
import { AppLayout } from './AppLayout';
import { useAuth } from './auth';

// Pages load on demand so the first paint only ships the shell and the login.
const DashboardPage = lazy(() => import('@/features/dashboard/DashboardPage').then((m) => ({ default: m.DashboardPage })));
const ProductsPage = lazy(() => import('@/features/inventory/ProductsPage').then((m) => ({ default: m.ProductsPage })));
const CategoriesPage = lazy(() => import('@/features/inventory/CategoriesPage').then((m) => ({ default: m.CategoriesPage })));
const StockPage = lazy(() => import('@/features/inventory/StockPage').then((m) => ({ default: m.StockPage })));
const MovementsPage = lazy(() => import('@/features/inventory/MovementsPage').then((m) => ({ default: m.MovementsPage })));
const OrdersPage = lazy(() => import('@/features/orders/OrdersPage').then((m) => ({ default: m.OrdersPage })));
const OrderDetailPage = lazy(() => import('@/features/orders/OrderDetailPage').then((m) => ({ default: m.OrderDetailPage })));
const PurchaseOrdersPage = lazy(() => import('@/features/orders/PurchaseOrdersPage').then((m) => ({ default: m.PurchaseOrdersPage })));
const PurchaseOrderDetailPage = lazy(() => import('@/features/orders/PurchaseOrderDetailPage').then((m) => ({ default: m.PurchaseOrderDetailPage })));
const ReportsPage = lazy(() => import('@/features/reports/ReportsPage').then((m) => ({ default: m.ReportsPage })));
const NotificationsPage = lazy(() => import('@/features/notifications/NotificationsPage').then((m) => ({ default: m.NotificationsPage })));
const RecipientsPage = lazy(() => import('@/features/notifications/RecipientsPage').then((m) => ({ default: m.RecipientsPage })));
const UsersPage = lazy(() => import('@/features/admin/UsersPage').then((m) => ({ default: m.UsersPage })));
const SettingsPage = lazy(() => import('@/features/admin/SettingsPage').then((m) => ({ default: m.SettingsPage })));
const RegisterPage = lazy(() => import('@/features/auth/RegisterPage').then((m) => ({ default: m.RegisterPage })));

function RequireAuth({ children }: { children: ReactNode }) {
  const { session, restoring } = useAuth();
  const location = useLocation();
  if (restoring) {
    return (
      <Center h="100vh">
        <Loader color="pine" />
      </Center>
    );
  }
  if (!session) return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  return <>{children}</>;
}

function RequireGuest({ children }: { children: ReactNode }) {
  const { session, restoring } = useAuth();
  if (restoring) return null;
  return session ? <Navigate to="/" replace /> : <>{children}</>;
}

function RequireOwner({ children }: { children: ReactNode }) {
  const { isOwner } = useAuth();
  if (!isOwner) {
    return (
<EmptyState title="Solo para propietarios" />
    );
  }
  return <>{children}</>;
}

export const router = createBrowserRouter([
  { path: '/login', element: <RequireGuest><LoginPage /></RequireGuest> },
  { path: '/register', element: <RequireGuest><Suspense fallback={null}><RegisterPage /></Suspense></RequireGuest> },
  {
    path: '/',
    element: <RequireAuth><AppLayout /></RequireAuth>,
    children: [
      { index: true, element: <DashboardPage /> },
      { path: 'stock', element: <StockPage /> },
      { path: 'products', element: <ProductsPage /> },
      { path: 'categories', element: <CategoriesPage /> },
      { path: 'movements', element: <MovementsPage /> },
      { path: 'orders', element: <OrdersPage /> },
      { path: 'orders/:id', element: <OrderDetailPage /> },
      { path: 'purchase-orders', element: <RequireOwner><PurchaseOrdersPage /></RequireOwner> },
      { path: 'purchase-orders/:id', element: <RequireOwner><PurchaseOrderDetailPage /></RequireOwner> },
      { path: 'reports', element: <ReportsPage /> },
      { path: 'notifications', element: <NotificationsPage /> },
      { path: 'notifications/recipients', element: <RecipientsPage /> },
      { path: 'users', element: <UsersPage /> },
      { path: 'settings', element: <SettingsPage /> },
      { path: '*', element: <EmptyState title="Página no encontrada" /> },
    ],
  },
]);
