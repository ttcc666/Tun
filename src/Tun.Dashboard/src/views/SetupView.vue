<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { api } from '../services/api'

const router = useRouter()
const password = ref('')
const confirmPassword = ref('')
const error = ref('')
const loading = ref(false)

const handleSubmit = async () => {
  error.value = ''

  if (password.value.length < 8) {
    error.value = '密码至少 8 位'
    return
  }

  if (password.value !== confirmPassword.value) {
    error.value = '两次密码不一致'
    return
  }

  loading.value = true
  try {
    await api.setup({ password: password.value })
    router.push('/')
  } catch (err) {
    error.value = err instanceof Error ? err.message : '初始化失败'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="min-h-screen bg-gray-50 flex items-center justify-center">
    <div class="bg-white p-8 rounded-lg shadow-md w-full max-w-md">
      <h1 class="text-2xl font-bold text-gray-900 mb-2">初始化管理员账号</h1>
      <p class="text-sm text-gray-500 mb-6">首次运行，请设置管理员密码</p>

      <form @submit.prevent="handleSubmit" class="space-y-4">
        <div>
          <label class="block text-sm text-gray-700 mb-1.5">密码</label>
          <input
            v-model="password"
            type="password"
            required
            class="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-teal-500"
            placeholder="至少 8 位"
          />
        </div>

        <div>
          <label class="block text-sm text-gray-700 mb-1.5">确认密码</label>
          <input
            v-model="confirmPassword"
            type="password"
            required
            class="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-teal-500"
            placeholder="再次输入密码"
          />
        </div>

        <div v-if="error" class="text-sm text-red-600">
          {{ error }}
        </div>

        <button
          type="submit"
          :disabled="loading"
          class="w-full px-4 py-2 bg-teal-600 text-white font-medium rounded-md hover:bg-teal-700 disabled:opacity-50 transition-colors"
        >
          {{ loading ? '初始化中...' : '完成初始化' }}
        </button>
      </form>
    </div>
  </div>
</template>
