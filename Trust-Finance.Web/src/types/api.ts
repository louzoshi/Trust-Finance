/** Mirrors ResultViewModel<T> from the API. */
export interface ApiResult<T> {
  data: T | null;
  errors: string[];
}

export interface Category {
  id: number;
  name: string;
  slug: string;
}

export interface Transaction {
  id: number;
  description: string;
  amount: number;
  date: string; // ISO 8601
  categoryId: number;
  userId: number;
}

/** Mirrors EditorTransactionViewModel. */
export interface TransactionPayload {
  description: string;
  amount: number;
  date: string;
  categoryId: number;
}

/** Mirrors EditorCategoryViewModel. */
export interface CategoryPayload {
  name: string;
  slug: string;
}

/** Mirrors LoginViewModel. */
export interface LoginPayload {
  email: string;
  password: string;
}

/** Mirrors RegisterUserViewModel. */
export interface RegisterPayload {
  name: string;
  email: string;
  password: string;
  image: string;
  slug: string;
}

export interface AuthUser {
  id: number;
  name: string;
  role: string;
}
