import { ComponentFixture, TestBed } from '@angular/core/testing';

import { UploadBatchDetails } from './upload-batch-details';

describe('UploadBatchDetails', () => {
  let component: UploadBatchDetails;
  let fixture: ComponentFixture<UploadBatchDetails>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UploadBatchDetails],
    }).compileComponents();

    fixture = TestBed.createComponent(UploadBatchDetails);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
