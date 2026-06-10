import { createRouter, createWebHistory } from 'vue-router'
import DashboardView from '../views/DashboardView.vue'
import LoginView from '../views/LoginView.vue'
import SetupView from '../views/SetupView.vue'
import { api } from '../services/api'

const router = createRouter({
  history: createWebHistory('/dashboard/'),
  routes: [
    {
      path: '/',
      name: 'dashboard',
      component: DashboardView,
      meta: { requiresAuth: true }
    },
    {
      path: '/login',
      name: 'login',
      component: LoginView
    },
    {
      path: '/setup',
      name: 'setup',
      component: SetupView
    }
  ]
})

router.beforeEach(async (to, _from, next) => {
  try {
    const status = await api.getAuthStatus()

    // Not initialized, redirect to setup
    if (!status.isInitialized && to.name !== 'setup') {
      return next({ name: 'setup' })
    }

    // Already initialized but on setup page, redirect to login or dashboard
    if (status.isInitialized && to.name === 'setup') {
      return next(status.isAuthenticated ? { name: 'dashboard' } : { name: 'login' })
    }

    // Requires auth but not authenticated, redirect to login
    if (to.meta.requiresAuth && !status.isAuthenticated) {
      return next({ name: 'login' })
    }

    // Already authenticated but on login page, redirect to dashboard
    if (status.isAuthenticated && to.name === 'login') {
      return next({ name: 'dashboard' })
    }

    next()
  } catch (error) {
    if (error instanceof Error && error.message === 'NEEDS_SETUP') {
      return next({ name: 'setup' })
    }
    next()
  }
})

export default router

