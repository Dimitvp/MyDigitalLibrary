// Mirrors the Application-layer DTOs (see src/MyDigitalLibrary.Application).
// Enums serialize as strings (JsonStringEnumConverter, Program.cs).

export type BookFormat = 'Physical' | 'Ebook' | 'Audiobook';
export type CoverType = 'Unknown' | 'Hardcover' | 'Paperback';
export type OwnershipStatus = 'Owned' | 'Borrowed' | 'LentOut' | 'Sold' | 'GivenAway';
export type AcquisitionMethod = 'Bought' | 'Gift' | 'Borrowed' | 'Inherited' | 'Downloaded';

export interface Money {
  amount: number;
  currencyCode: string;
}

export interface Acquisition {
  acquiredOn: string; // yyyy-MM-dd
  method: AcquisitionMethod;
  price: Money | null;
  source: string | null;
}

export interface PhysicalLocation {
  room: string | null;
  shelf: string | null;
  box: string | null;
}

export interface LibraryItem {
  id: string;
  userId: string;
  editionId: string;
  format: BookFormat;
  status: OwnershipStatus;
  acquisition: Acquisition;
  location: PhysicalLocation | null;
  personalNote: string | null;
  workTitle: string;
  authorNames: readonly string[];
  coverImageUrl: string | null;
}

export interface CreateWorkInput {
  title: string;
  originalTitle: string | null;
  description: string | null;
  firstPublicationYear: number | null;
  authorNames: readonly string[] | null;
  seriesName: string | null;
  seriesPosition: number | null;
}

export interface NestedEditionInput {
  isbn13: string | null;
  publisher: string | null;
  language: string | null;
  translator: string | null;
  publicationYear: number | null;
  pageCount: number | null;
  coverType: CoverType | null;
  narrator: string | null;
  durationMinutes: number | null;
}

export interface CreateLibraryItemRequest {
  editionId: string | null;
  work: CreateWorkInput | null;
  edition: NestedEditionInput | null;
  format: BookFormat;
  status: OwnershipStatus | null;
  acquisition: Acquisition;
  location: PhysicalLocation | null;
}

export interface Edition {
  id: string;
  workId: string;
  format: BookFormat;
  isbn13: string | null;
  publisher: string | null;
  language: string | null;
  translator: string | null;
  publicationYear: number | null;
  pageCount: number | null;
  coverType: CoverType;
  coverImageUrl: string | null;
  narrator: string | null;
  durationMinutes: number | null;
}

export interface AuthorSummary {
  id: string;
  fullName: string;
}

export interface WorkDetail {
  id: string;
  title: string;
  originalTitle: string | null;
  description: string | null;
  firstPublicationYear: number | null;
  seriesId: string | null;
  seriesPosition: number | null;
  authors: readonly AuthorSummary[];
}

export interface WishlistEntry {
  id: string;
  userId: string;
  workId: string;
  preferredEditionId: string | null;
  desiredFormat: BookFormat;
  priority: number;
  maxPrice: Money | null;
  note: string | null;
  addedOn: string;
  isFulfilled: boolean;
  workTitle: string;
  authorNames: readonly string[];
}

export interface PagedResult<T> {
  items: readonly T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  errorCode?: string;
  [key: string]: unknown;
}
