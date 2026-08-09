import { Routes } from '@angular/router';
import { Home } from './home/home';
import { CreateUploadBatch } from './create-upload-batch/create-upload-batch';
import { UploadBatchDetails } from './upload-batch-details/upload-batch-details';

export const routes: Routes = [
  {
    path: '',
    component: Home,
  },
  {
    path: 'create-upload-batch',
    component: CreateUploadBatch,
  },
  {
    path: 'upload-batches',
    component: UploadBatchDetails,
  },
  {
    path: 'upload-batches/:id',
    component: UploadBatchDetails,
  },
];
